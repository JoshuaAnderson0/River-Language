#[derive(Clone)]
#[derive(Copy)]
pub struct CallFrame {
    pub register_base: usize,
    pub previous_cp: usize,
    pub previous_ip: usize,
}

impl CallFrame {
    pub fn new(register_base: usize, previous_cp: usize, previous_ip: usize) -> Self {
        Self { register_base, previous_cp, previous_ip }
    }
}
