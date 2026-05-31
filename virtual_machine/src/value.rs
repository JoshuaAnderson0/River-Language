#[derive(Debug)]
#[derive(Clone)]
#[derive(Copy)]
pub enum ValueType {
    Int,
    UInt,
    Float,
    Bool,
    Char,
    Object
}

#[derive(Debug)]
#[derive(Clone)]
#[derive(Copy)]
pub struct Value {
    pub _value: isize,
    pub _type: ValueType,
}

impl Value {
    pub fn new_int(value: isize) -> Self {
        Self { _value: value, _type: ValueType::Int } 
    }

    pub fn new_uint(value: usize) -> Self {
        Self { _value: value as isize, _type: ValueType::UInt } 
    }

    #[cfg(target_pointer_width = "64")]
    pub fn new_float(value: f64) -> Self {
        Self { _value: value.to_bits() as isize, _type: ValueType::Float } 
    }

    #[cfg(not(target_pointer_width = "64"))]
    pub fn new_float(value: f32) -> Self {
        Self { _value: value.to_bits() as isize, _type: ValueType::Float } 
    }

    pub fn new_bool(value: bool) -> Self {
        Self { _value: value as isize, _type: ValueType::Bool } 
    }
}
