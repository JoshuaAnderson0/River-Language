use std::collections::HashMap;

use crate::value::Value;
use crate::value::ValueType;
use crate::chunk::Chunk;
use crate::call_frame::CallFrame;
use crate::opcode;

type InstructionHandler = fn(&mut VirtualMachine) -> InstructionResult;

enum InstructionResult {
    Ok,
    CompileError,
    RuntimeError,
}

pub struct VirtualMachine {
    // contigious array of memory that is treated as registers
    registers: Vec<Value>,

    // Represents the last index in the registers that is activley being used
    // by any of cal frames
    register_top: usize,

    // Stack of all the call frames
    call_stack: Vec<CallFrame>,

    // Chunks represent the context of each compiled function
    chunks: Vec<Chunk>,

    // The chunk pointer
    cp: usize,

    // The instruction pointer
    ip: usize,

    dispatch_table: HashMap<u8, InstructionHandler>,
}

macro_rules! int_binop {
    ($name:ident, $op:ident) => {
        fn $name(&mut self) -> InstructionResult {
            let frame = self.call_stack.last().unwrap();
            let chunk = &self.chunks[self.cp];

            let r0 = chunk.read_argument(&mut self.ip);
            let r1 = chunk.read_argument(&mut self.ip);
            let r2 = chunk.read_argument(&mut self.ip);

            let left: isize = self.registers[frame.register_base + r0]._value;
            let right: isize = self.registers[frame.register_base + r1]._value;

            self.registers[frame.register_base + r2] = Value::new_int(left.$op(right));
            InstructionResult::Ok
        }
    };
}

macro_rules! uint_binop {
    ($name:ident, $op:ident) => {
        fn $name(&mut self) -> InstructionResult {
            let frame = self.call_stack.last().unwrap();
            let chunk = &self.chunks[self.cp];

            let r0 = chunk.read_argument(&mut self.ip);
            let r1 = chunk.read_argument(&mut self.ip);
            let r2 = chunk.read_argument(&mut self.ip);

            let left: isize = self.registers[frame.register_base + r0]._value;
            let right: isize = self.registers[frame.register_base + r1]._value;

            let result = (left as usize).$op(right as usize);
            self.registers[frame.register_base + r2] = Value::new_uint(result);
            InstructionResult::Ok
        }
    };
}

macro_rules! float_binop {
    ($name:ident, $op:tt) => {
        fn $name(&mut self) -> InstructionResult {
            let frame = self.call_stack.last().unwrap();
            let chunk = &self.chunks[self.cp];

            let r0 = chunk.read_argument(&mut self.ip);
            let r1 = chunk.read_argument(&mut self.ip);
            let r2 = chunk.read_argument(&mut self.ip);

            let left: isize = self.registers[frame.register_base + r0]._value;
            let right: isize = self.registers[frame.register_base + r1]._value;

            #[cfg(target_pointer_width = "64")]
            let result = {
                let fa = f64::from_bits(left as u64);
                let fb = f64::from_bits(right as u64);
                fa $op fb
            };
            
            #[cfg(not(target_pointer_width = "64"))]
            let result = {
                let fa = f32::from_bits(left as u32);
                let fb = f32::from_bits(right as u32);
                fa $op fb
            };

            self.registers[frame.register_base + r2] = Value::new_float(result);
            InstructionResult::Ok
        }
    };
}


impl VirtualMachine {
    pub fn new() -> Self {
        let mut vm = Self {
            registers: Vec::new(),
            register_top: 0,
            call_stack: Vec::new(),
            chunks: Vec::new(),
            cp: 0,
            ip: 0,
            dispatch_table: HashMap::new(),
        };

        vm.dispatch_table.insert(opcode::RETURN,    VirtualMachine::op_return);
        vm.dispatch_table.insert(opcode::CALL,      VirtualMachine::op_call);
        vm.dispatch_table.insert(opcode::PRINT,     VirtualMachine::op_print);
        vm.dispatch_table.insert(opcode::CONSTANT,  VirtualMachine::op_constant);
        vm.dispatch_table.insert(opcode::ADD_INT,   VirtualMachine::op_add_int);
        vm.dispatch_table.insert(opcode::SUB_INT,   VirtualMachine::op_sub_int);
        vm.dispatch_table.insert(opcode::MUL_INT,   VirtualMachine::op_mul_int);
        vm.dispatch_table.insert(opcode::DIV_INT,   VirtualMachine::op_div_int);
        vm.dispatch_table.insert(opcode::ADD_UINT,  VirtualMachine::op_add_uint);
        vm.dispatch_table.insert(opcode::SUB_UINT,  VirtualMachine::op_sub_uint);
        vm.dispatch_table.insert(opcode::MUL_UINT,  VirtualMachine::op_mul_uint);
        vm.dispatch_table.insert(opcode::DIV_UINT,  VirtualMachine::op_div_uint);
        vm.dispatch_table.insert(opcode::ADD_FLOAT, VirtualMachine::op_add_float);
        vm.dispatch_table.insert(opcode::SUB_FLOAT, VirtualMachine::op_sub_float);
        vm.dispatch_table.insert(opcode::MUL_FLOAT, VirtualMachine::op_mul_float);
        vm.dispatch_table.insert(opcode::DIV_FLOAT, VirtualMachine::op_div_float);

        vm
    }

    pub fn push_chunk(&mut self, chunk: Chunk) -> usize {
        self.chunks.push(chunk);
        self.chunks.len() - 1
    }

    fn push_call_frame(&mut self, cp: usize) {
        let chunk = &self.chunks[cp];
        let old_len = self.registers.len();
        let new_len = self.register_top + chunk.register_size;

        if old_len < new_len {
            self.registers.resize(new_len, Value::new_int(0));
        }

        self.call_stack.push(CallFrame::new(
            self.register_top,
            self.cp,
            self.ip,
        ));

        self.register_top += chunk.register_size;
        self.cp = cp;
        self.ip = 0;
    }

    pub fn run(&mut self, chunk: Chunk) {
        let index = self.push_chunk(chunk);
        self.push_call_frame(index);

        loop {
            if self.call_stack.is_empty() {
                break;
            }

            let result = self.execute_instruction();
            match result {
                InstructionResult::Ok => { },

                InstructionResult::CompileError => {
                    println!("compile error");
                    break;
                },

                InstructionResult::RuntimeError => {
                    println!("runtime error");
                    break;
                },
            }
        }
    }

    fn execute_instruction(&mut self) -> InstructionResult {
        if self.call_stack.is_empty() {
            return InstructionResult::RuntimeError;
        }

        let chunk = &self.chunks[self.cp];
        let opcode = chunk.read_instruction(&mut self.ip);

        match self.dispatch_table.get(&opcode) {
            Some(handler) => handler(self),
            None => InstructionResult::CompileError,
        }
    }

    fn op_return(&mut self) -> InstructionResult {

        let chunk = &self.chunks[self.cp];
        if let Some(frame) = self.call_stack.pop() {
            self.cp = frame.previous_cp;
            self.ip = frame.previous_ip;
            self.register_top -= chunk.register_size;
        }

        InstructionResult::Ok
    }

    fn op_call(&mut self) -> InstructionResult {
        let chunk = &self.chunks[self.cp];
        let index = chunk.read_argument(&mut self.ip);

        self.push_call_frame(index);
        InstructionResult::Ok
    }

    fn op_print(&mut self) -> InstructionResult {
        let frame = self.call_stack.last().unwrap();
        let chunk = &self.chunks[self.cp];

        let r0 = chunk.read_argument(&mut self.ip);
        let value = self.registers[frame.register_base + r0];

        match value._type {
            ValueType::Int => println!("{}", value._value),
            ValueType::UInt => println!("{}", value._value as usize),
            ValueType::Float => {
                #[cfg(target_pointer_width = "64")]
                {
                    let f = f64::from_bits(value._value as u64);
                    println!("{}", f);
                }
        
                #[cfg(not(target_pointer_width = "64"))]
                {
                    let f = f32::from_bits(value._value as u32);
                    println!("{}", f);
                }
            }
            ValueType::Bool => println!("{}", value._value != 0),
            ValueType::Char => todo!(),
            ValueType::Object => todo!(),
        }

        InstructionResult::Ok
    }

    fn op_constant(&mut self) -> InstructionResult {
        let frame = self.call_stack.last().unwrap();
        let chunk = &self.chunks[self.cp];

        let index = chunk.read_argument(&mut self.ip);
        let r0 = chunk.read_argument(&mut self.ip);

        self.registers[frame.register_base + r0] = chunk.constants[index];

        InstructionResult::Ok
    }

    int_binop!(op_add_int, wrapping_add);
    int_binop!(op_sub_int, wrapping_sub);
    int_binop!(op_mul_int, wrapping_mul);
    int_binop!(op_div_int, wrapping_div);

    uint_binop!(op_add_uint, wrapping_add);
    uint_binop!(op_sub_uint, wrapping_sub);
    uint_binop!(op_mul_uint, wrapping_mul);
    uint_binop!(op_div_uint, wrapping_div);

    float_binop!(op_add_float, +);
    float_binop!(op_sub_float, -);
    float_binop!(op_mul_float, *);
    float_binop!(op_div_float, /);
}

impl Default for VirtualMachine {
    fn default() -> Self {
        Self::new()
    }
}
