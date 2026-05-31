use crate::chunk::Chunk;
use crate::opcode;
use crate::value::Value;
use crate::vm::VirtualMachine;

const LEFT_REG: usize = 0;
const RIGHT_REG: usize = 1;
const RESULT_REG: usize = 2;

fn run_logical_op(op: u8, left_value: bool, right_value: bool) -> bool {
    let mut chunk = Chunk::new("test", 3);

    let left_const = chunk.push_constant(Value::new_bool(left_value));
    let right_const = chunk.push_constant(Value::new_bool(right_value));

    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[left_const, LEFT_REG]);
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[right_const, RIGHT_REG]);
    chunk.push_instruction(op).with_arguments(&[LEFT_REG, RIGHT_REG, RESULT_REG]);
    chunk.push_instruction(opcode::RETURN);

    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    vm.get_register(RESULT_REG)._value != 0
}

#[test]
fn logical_and_with_both_true_returns_true() {
    // Arrange
    let left: bool = true;
    let right: bool = true;

    // Act
    let result = run_logical_op(opcode::LOGICAL_AND, left, right);

    // Assert
    assert!(result);
}

#[test]
fn logical_and_with_left_false_returns_false() {
    // Arrange
    let left: bool = false;
    let right: bool = true;

    // Act
    let result = run_logical_op(opcode::LOGICAL_AND, left, right);

    // Assert
    assert!(!result);
}

#[test]
fn logical_and_with_right_false_returns_false() {
    // Arrange
    let left: bool = true;
    let right: bool = false;

    // Act
    let result = run_logical_op(opcode::LOGICAL_AND, left, right);

    // Assert
    assert!(!result);
}

#[test]
fn logical_and_with_both_false_returns_false() {
    // Arrange
    let left: bool = false;
    let right: bool = false;

    // Act
    let result = run_logical_op(opcode::LOGICAL_AND, left, right);

    // Assert
    assert!(!result);
}

#[test]
fn logical_or_with_both_true_returns_true() {
    // Arrange
    let left: bool = true;
    let right: bool = true;

    // Act
    let result = run_logical_op(opcode::LOGICAL_OR, left, right);

    // Assert
    assert!(result);
}

#[test]
fn logical_or_with_left_true_returns_true() {
    // Arrange
    let left: bool = true;
    let right: bool = false;

    // Act
    let result = run_logical_op(opcode::LOGICAL_OR, left, right);

    // Assert
    assert!(result);
}

#[test]
fn logical_or_with_right_true_returns_true() {
    // Arrange
    let left: bool = false;
    let right: bool = true;

    // Act
    let result = run_logical_op(opcode::LOGICAL_OR, left, right);

    // Assert
    assert!(result);
}

#[test]
fn logical_or_with_both_false_returns_false() {
    // Arrange
    let left: bool = false;
    let right: bool = false;

    // Act
    let result = run_logical_op(opcode::LOGICAL_OR, left, right);

    // Assert
    assert!(!result);
}
