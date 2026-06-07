// ARGUMENTS: (none)
pub const RETURN: u8 = 0;

// ARGUMENTS: register_index (pointing to chunk index)
pub const CALL: u8 = 1;

// ARGUMENTS: register_index
pub const PRINT: u8 = 2;

// ARGUMENTS: constant_index, destination_register_index
pub const CONSTANT: u8 = 3;

// ARGUMENTS: left_register_index, right_register_index, destination_register_index
pub const ADD_INT: u8 = 4;
pub const SUB_INT: u8 = 5;
pub const MUL_INT: u8 = 6;
pub const DIV_INT: u8 = 7;

pub const ADD_UINT: u8 = 8;
pub const SUB_UINT: u8 = 9;
pub const MUL_UINT: u8 = 10;
pub const DIV_UINT: u8 = 11;

pub const ADD_FLOAT: u8 = 12;
pub const SUB_FLOAT: u8 = 13;
pub const MUL_FLOAT: u8 = 14;
pub const DIV_FLOAT: u8 = 15;

pub const EQUAL: u8 = 16;
pub const NOT_EQUAL: u8 = 17;
pub const GREATER: u8 = 18;
pub const GREATE_EQUAL: u8 = 19;
pub const LESS: u8 = 20;
pub const LESS_EQUAL: u8 = 21;
pub const LOGICAL_AND: u8 = 22;
pub const LOGICAL_OR: u8 = 23;

pub const BINARY_AND: u8 = 24;
pub const BINARY_OR: u8 = 25;
pub const BINARY_LSHIFT: u8 = 26;
pub const BINARY_RSHIFT: u8 = 27;
pub const BINARY_XOR: u8 = 28;

pub const COUNT: usize = 29;
