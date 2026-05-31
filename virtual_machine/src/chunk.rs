//use std::fmt::Formatter;
//use std::fmt::Debug;
//use std::fmt::Result;

use crate::value::Value;

pub struct Chunk {
    pub name: &'static str,
    pub bytecode: Vec<u8>,
    pub constants: Vec<Value>,
    pub register_size: usize,
}

impl Chunk {
    pub fn new(name: &'static str, register_size: usize) -> Self {
        Self { 
            name,
            bytecode: Vec::new(),
            constants: Vec::new(),
            register_size
        }
    }

    pub fn push_instruction(&mut self, opcode: u8) -> &mut Self {
        self.bytecode.push(opcode);
        self
    }

    
    pub fn with_arguments(&mut self, args: &[usize])
    {
        for arg in args {
            let mut value: usize = *arg;

            loop {
                let mut byte: u8 = (value & 0x7F) as u8;
                value >>= 7;

                if value > 0 {
                    byte |= 0x80;
                    self.bytecode.push(byte);
                } else {
                    self.bytecode.push(byte);
                    break;
                }
            }
        }
    }

    pub fn push_constant(&mut self, value: Value) -> usize {
        self.constants.push(value);
        self.constants.len() - 1
    }

    pub fn read_instruction(&self, index: &mut usize) -> u8 {
        let instruction = self.bytecode[*index];
        *index += 1;
        instruction 
    }

    pub fn read_argument(&self, index: &mut usize) -> usize
    {
        let mut result: usize = 0;
        let mut shift: usize = 0;

        loop {
            let byte: u8 = self.bytecode[*index];
            *index += 1;

            let value_part: usize = (byte & 0x7F) as usize;
            result |= value_part << shift;

            if (byte & 0x80) == 0 {
                break;
            }

            shift += 7;
        }

        result
    }

//   fn write_instruction(&self, instruction: &u8, f: &mut Formatter<'_>) 
//    {
//        let spacing = "    ";
//        match instruction {
//            opcode::RETURN => {
//                let _ = writeln!(f, "{}{:?}", spacing, instruction);
//            },

//            opcode::ADD => {
//                let _ = writeln!(f, "{}{} {} {} {}", spacing, stringify!(opcode::Add), r0, r1, r2);
//            },
//            opcode::PRINT => {
//                let _ = writeln!(f, "{}{} {}", spacing, stringify!(opcode::Print), r0);
//            },
//            opcode::CALL => {
//                let _ = writeln!(f, "{}{} {}", spacing, stringify!(OpCode::Call), index);
//            },
//            opcode::CONSTANT => {
//                let value = self.constants[*index];
//                let _ = writeln!(f, "{}{} {:?} {}", spacing, stringify!(opcode::constant), value, r0);
//            },
//       }
//   }
}

impl Default for Chunk {
    fn default() -> Self {
        Self::new("_default", 0)
    }
}

//impl Debug for Chunk {
//    fn fmt(&self, f: &mut Formatter<'_>) -> Result {
//        let _ = writeln!(f, "== Chunk ==");
//
//        _ = writeln!(f, "  Instructions ->");
//        for bytecode in self.bytecode.iter()
//        {
//            self.write_instruction(bytecode, f);
//        }

//        _ = writeln!(f, "\n  Constants ->");
//        for (index, value) in self.constants.iter().enumerate()
//        {
//            _ = writeln!(f, "    {}: {:?}", index, value);
//        }
//        write!(f, "")
//    }
//}
