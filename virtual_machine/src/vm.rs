use std::ops::{BitAnd, BitOr, BitXor, Shl, Shr};

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
    registers: Vec<Value>,
    register_top: usize,
    call_stack: Vec<CallFrame>,
    chunks: Vec<Chunk>,
    cp: usize,
    ip: usize,
    dispatch_table: [InstructionHandler; opcode::COUNT],
}

macro_rules! int_binop {
    ($name:ident, $op:ident) => {
        fn $name(&mut self) -> InstructionResult {
            let r0 = self.read_arg();
            let r1 = self.read_arg();
            let r2 = self.read_arg();
            let left = self.get_register(r0)._value;
            let right = self.get_register(r1)._value;
            self.set_register(r2, Value::new_int(left.$op(right)));
            InstructionResult::Ok
        }
    };
}

macro_rules! uint_binop {
    ($name:ident, $op:ident) => {
        fn $name(&mut self) -> InstructionResult {
            let r0 = self.read_arg();
            let r1 = self.read_arg();
            let r2 = self.read_arg();
            let left = self.get_register(r0)._value as usize;
            let right = self.get_register(r1)._value as usize;
            self.set_register(r2, Value::new_uint(left.$op(right)));
            InstructionResult::Ok
        }
    };
}

macro_rules! float_binop {
    ($name:ident, $op:tt) => {
        fn $name(&mut self) -> InstructionResult {
            let r0 = self.read_arg();
            let r1 = self.read_arg();
            let r2 = self.read_arg();
            let left = self.get_register(r0)._value;
            let right = self.get_register(r1)._value;

            #[cfg(target_pointer_width = "64")]
            let result = f64::from_bits(left as u64) $op f64::from_bits(right as u64);

            #[cfg(not(target_pointer_width = "64"))]
            let result = f32::from_bits(left as u32) $op f32::from_bits(right as u32);

            self.set_register(r2, Value::new_float(result));
            InstructionResult::Ok
        }
    };
}

macro_rules! comparison_binop {
    ($name:ident, $op:tt) => {
        fn $name(&mut self) -> InstructionResult {
            let r0 = self.read_arg();
            let r1 = self.read_arg();
            let r2 = self.read_arg();
            let left = self.get_register(r0)._value;
            let right = self.get_register(r1)._value;
            self.set_register(r2, Value::new_bool(left $op right));
            InstructionResult::Ok
        }
    };
}

macro_rules! logical_binop {
    ($name:ident, $op:tt) => {
        fn $name(&mut self) -> InstructionResult {
            let r0 = self.read_arg();
            let r1 = self.read_arg();
            let r2 = self.read_arg();
            let left = self.get_register(r0)._value != 0;
            let right = self.get_register(r1)._value != 0;
            self.set_register(r2, Value::new_bool(left $op right));
            InstructionResult::Ok
        }
    };
}

impl VirtualMachine {
    #[inline(always)]
    fn read_arg(&mut self) -> usize {
        self.chunks[self.cp].read_argument(&mut self.ip)
    }

    #[inline(always)]
    fn current_base(&self) -> usize {
        self.call_stack.last().map_or(0, |f| f.register_base)
    }

    #[inline(always)]
    pub fn get_register(&self, offset: usize) -> Value {
        self.registers[self.current_base() + offset]
    }

    #[inline(always)]
    pub fn set_register(&mut self, offset: usize, val: Value) {
        let base = self.current_base();
        self.registers[base + offset] = val;
    }

    pub fn new() -> Self {
        Self {
            registers: Vec::new(),
            register_top: 0,
            call_stack: Vec::new(),
            chunks: Vec::new(),
            cp: 0,
            ip: 0,
            dispatch_table: [
                VirtualMachine::op_return,
                VirtualMachine::op_call,
                VirtualMachine::op_print,
                VirtualMachine::op_constant,
                VirtualMachine::op_add_int,
                VirtualMachine::op_sub_int,
                VirtualMachine::op_mul_int,
                VirtualMachine::op_div_int,
                VirtualMachine::op_add_uint,
                VirtualMachine::op_sub_uint,
                VirtualMachine::op_mul_uint,
                VirtualMachine::op_div_uint,
                VirtualMachine::op_add_float,
                VirtualMachine::op_sub_float,
                VirtualMachine::op_mul_float,
                VirtualMachine::op_div_float,
                VirtualMachine::op_equal,
                VirtualMachine::op_not_equal,
                VirtualMachine::op_greater,
                VirtualMachine::op_greater_equal,
                VirtualMachine::op_less,
                VirtualMachine::op_less_equal,
                VirtualMachine::op_logical_and,
                VirtualMachine::op_logical_or,
                VirtualMachine::op_binary_and,
                VirtualMachine::op_binary_or,
                VirtualMachine::op_binary_lshift,
                VirtualMachine::op_binary_rshift,
                VirtualMachine::op_binary_xor,
            ],
        }
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

        if (opcode as usize) < opcode::COUNT {
            self.dispatch_table[opcode as usize](self)
        } else {
            InstructionResult::CompileError
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
        let index = self.read_arg();
        self.push_call_frame(index);
        InstructionResult::Ok
    }

    fn op_print(&mut self) -> InstructionResult {
        let r0 = self.read_arg();
        let value = self.get_register(r0);

        match value._type {
            ValueType::Int => println!("{}", value._value),
            ValueType::UInt => println!("{}", value._value as usize),
            ValueType::Float => {
                #[cfg(target_pointer_width = "64")]
                println!("{}", f64::from_bits(value._value as u64));
                #[cfg(not(target_pointer_width = "64"))]
                println!("{}", f32::from_bits(value._value as u32));
            }
            ValueType::Bool => println!("{}", value._value != 0),
            ValueType::Char => todo!(),
            ValueType::Object => todo!(),
        }

        InstructionResult::Ok
    }

    fn op_constant(&mut self) -> InstructionResult {
        let index = self.read_arg();
        let r0 = self.read_arg();
        let val = self.chunks[self.cp].constants[index];
        self.set_register(r0, val);
        InstructionResult::Ok
    }

    int_binop!(op_add_int, wrapping_add);
    int_binop!(op_sub_int, wrapping_sub);
    int_binop!(op_mul_int, wrapping_mul);
    int_binop!(op_div_int, wrapping_div);
    int_binop!(op_binary_and, bitand);
    int_binop!(op_binary_or, bitor);
    int_binop!(op_binary_xor, bitxor);
    int_binop!(op_binary_lshift, shl);
    int_binop!(op_binary_rshift, shr);

    uint_binop!(op_add_uint, wrapping_add);
    uint_binop!(op_sub_uint, wrapping_sub);
    uint_binop!(op_mul_uint, wrapping_mul);
    uint_binop!(op_div_uint, wrapping_div);

    float_binop!(op_add_float, +);
    float_binop!(op_sub_float, -);
    float_binop!(op_mul_float, *);
    float_binop!(op_div_float, /);

    comparison_binop!(op_equal, ==);
    comparison_binop!(op_not_equal, !=);
    comparison_binop!(op_greater, >);
    comparison_binop!(op_greater_equal, >=);
    comparison_binop!(op_less, <);
    comparison_binop!(op_less_equal, <=);

    logical_binop!(op_logical_and, &&);
    logical_binop!(op_logical_or, ||);
}

impl Default for VirtualMachine {
    fn default() -> Self {
        Self::new()
    }
}