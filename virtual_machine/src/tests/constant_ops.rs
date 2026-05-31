use crate::chunk::Chunk;
use crate::opcode;
use crate::value::{Value, ValueType};
use crate::vm::VirtualMachine;

#[test]
fn constant_with_int_value_loads_int() {
    // Arrange
    let mut chunk = Chunk::new("test", 1);
    let c0 = chunk.push_constant(Value::new_int(42));
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c0, 0]);
    chunk.push_instruction(opcode::RETURN);

    // Act
    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    // Assert
    let val = vm.get_register(0);
    assert_eq!(val._value, 42);
    assert!(matches!(val._type, ValueType::Int));
}

#[test]
fn constant_with_uint_value_loads_uint() {
    // Arrange
    let mut chunk = Chunk::new("test", 1);
    let c0 = chunk.push_constant(Value::new_uint(12345));
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c0, 0]);
    chunk.push_instruction(opcode::RETURN);

    // Act
    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    // Assert
    let val = vm.get_register(0);
    assert_eq!(val._value as usize, 12345);
    assert!(matches!(val._type, ValueType::UInt));
}

#[test]
fn constant_with_float_value_loads_float() {
    // Arrange
    let mut chunk = Chunk::new("test", 1);
    let c0 = chunk.push_constant(Value::new_float(3.14159));
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c0, 0]);
    chunk.push_instruction(opcode::RETURN);

    // Act
    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    // Assert
    let val = vm.get_register(0);
    let f = f64::from_bits(val._value as u64);
    assert!((f - 3.14159).abs() < 1e-10);
    assert!(matches!(val._type, ValueType::Float));
}

#[test]
fn constant_with_true_value_loads_bool() {
    // Arrange
    let mut chunk = Chunk::new("test", 1);
    let c0 = chunk.push_constant(Value::new_bool(true));
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c0, 0]);
    chunk.push_instruction(opcode::RETURN);

    // Act
    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    // Assert
    let val = vm.get_register(0);
    assert_eq!(val._value, 1);
    assert!(matches!(val._type, ValueType::Bool));
}

#[test]
fn constant_with_false_value_loads_bool() {
    // Arrange
    let mut chunk = Chunk::new("test", 1);
    let c0 = chunk.push_constant(Value::new_bool(false));
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c0, 0]);
    chunk.push_instruction(opcode::RETURN);

    // Act
    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    // Assert
    let val = vm.get_register(0);
    assert_eq!(val._value, 0);
    assert!(matches!(val._type, ValueType::Bool));
}

#[test]
fn constant_with_negative_int_loads_negative() {
    // Arrange
    let mut chunk = Chunk::new("test", 1);
    let c0 = chunk.push_constant(Value::new_int(-999));
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c0, 0]);
    chunk.push_instruction(opcode::RETURN);

    // Act
    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    // Assert
    let val = vm.get_register(0);
    assert_eq!(val._value, -999);
}

#[test]
fn constant_with_multiple_values_loads_all() {
    // Arrange
    let mut chunk = Chunk::new("test", 3);
    let c0 = chunk.push_constant(Value::new_int(10));
    let c1 = chunk.push_constant(Value::new_int(20));
    let c2 = chunk.push_constant(Value::new_int(30));
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c0, 0]);
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c1, 1]);
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c2, 2]);
    chunk.push_instruction(opcode::RETURN);

    // Act
    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    // Assert
    assert_eq!(vm.get_register(0)._value, 10);
    assert_eq!(vm.get_register(1)._value, 20);
    assert_eq!(vm.get_register(2)._value, 30);
}

#[test]
fn constant_to_same_register_overwrites() {
    // Arrange
    let mut chunk = Chunk::new("test", 1);
    let c0 = chunk.push_constant(Value::new_int(100));
    let c1 = chunk.push_constant(Value::new_int(200));
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c0, 0]);
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c1, 0]);
    chunk.push_instruction(opcode::RETURN);

    // Act
    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    // Assert
    assert_eq!(vm.get_register(0)._value, 200);
}

#[test]
fn constant_with_large_index_loads_correctly() {
    // Arrange
    let mut chunk = Chunk::new("test", 1);
    for i in 0..200 {
        chunk.push_constant(Value::new_int(i));
    }
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[199, 0]);
    chunk.push_instruction(opcode::RETURN);

    // Act
    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    // Assert
    assert_eq!(vm.get_register(0)._value, 199);
}
