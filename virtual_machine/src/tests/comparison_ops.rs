use crate::chunk::Chunk;
use crate::opcode;
use crate::value::Value;
use crate::vm::VirtualMachine;

const LEFT_REG: usize = 0;
const RIGHT_REG: usize = 1;
const RESULT_REG: usize = 2;

fn run_comparison_op(op: u8, left_value: isize, right_value: isize) -> bool {
    let mut chunk = Chunk::new("test", 3);

    let left_const = chunk.push_constant(Value::new_int(left_value));
    let right_const = chunk.push_constant(Value::new_int(right_value));

    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[left_const, LEFT_REG]);
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[right_const, RIGHT_REG]);
    chunk.push_instruction(op).with_arguments(&[LEFT_REG, RIGHT_REG, RESULT_REG]);
    chunk.push_instruction(opcode::RETURN);

    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    vm.get_register(RESULT_REG)._value != 0
}

#[test]
fn equal_with_same_values_returns_true() {
    // Arrange
    let value: isize = 42;

    // Act
    let result = run_comparison_op(opcode::EQUAL, value, value);

    // Assert
    assert!(result);
}

#[test]
fn equal_with_different_values_returns_false() {
    // Arrange
    let left: isize = 42;
    let right: isize = 43;

    // Act
    let result = run_comparison_op(opcode::EQUAL, left, right);

    // Assert
    assert!(!result);
}

#[test]
fn equal_with_zero_values_returns_true() {
    // Arrange
    let zero: isize = 0;

    // Act
    let result = run_comparison_op(opcode::EQUAL, zero, zero);

    // Assert
    assert!(result);
}

#[test]
fn equal_with_negative_same_values_returns_true() {
    // Arrange
    let negative: isize = -100;

    // Act
    let result = run_comparison_op(opcode::EQUAL, negative, negative);

    // Assert
    assert!(result);
}

#[test]
fn not_equal_with_different_values_returns_true() {
    // Arrange
    let left: isize = 42;
    let right: isize = 43;

    // Act
    let result = run_comparison_op(opcode::NOT_EQUAL, left, right);

    // Assert
    assert!(result);
}

#[test]
fn not_equal_with_same_values_returns_false() {
    // Arrange
    let value: isize = 42;

    // Act
    let result = run_comparison_op(opcode::NOT_EQUAL, value, value);

    // Assert
    assert!(!result);
}

#[test]
fn not_equal_with_positive_and_negative_returns_true() {
    // Arrange
    let positive: isize = 5;
    let negative: isize = -5;

    // Act
    let result = run_comparison_op(opcode::NOT_EQUAL, positive, negative);

    // Assert
    assert!(result);
}

#[test]
fn greater_with_larger_left_returns_true() {
    // Arrange
    let larger: isize = 100;
    let smaller: isize = 50;

    // Act
    let result = run_comparison_op(opcode::GREATER, larger, smaller);

    // Assert
    assert!(result);
}

#[test]
fn greater_with_smaller_left_returns_false() {
    // Arrange
    let smaller: isize = 50;
    let larger: isize = 100;

    // Act
    let result = run_comparison_op(opcode::GREATER, smaller, larger);

    // Assert
    assert!(!result);
}

#[test]
fn greater_with_equal_values_returns_false() {
    // Arrange
    let value: isize = 42;

    // Act
    let result = run_comparison_op(opcode::GREATER, value, value);

    // Assert
    assert!(!result);
}

#[test]
fn greater_with_negative_values_compares_correctly() {
    // Arrange
    let less_negative: isize = -5;
    let more_negative: isize = -10;

    // Act
    let result = run_comparison_op(opcode::GREATER, less_negative, more_negative);

    // Assert
    assert!(result);
}

#[test]
fn greater_equal_with_larger_left_returns_true() {
    // Arrange
    let larger: isize = 100;
    let smaller: isize = 50;

    // Act
    let result = run_comparison_op(opcode::GREATE_EQUAL, larger, smaller);

    // Assert
    assert!(result);
}

#[test]
fn greater_equal_with_equal_values_returns_true() {
    // Arrange
    let value: isize = 42;

    // Act
    let result = run_comparison_op(opcode::GREATE_EQUAL, value, value);

    // Assert
    assert!(result);
}

#[test]
fn greater_equal_with_smaller_left_returns_false() {
    // Arrange
    let smaller: isize = 50;
    let larger: isize = 100;

    // Act
    let result = run_comparison_op(opcode::GREATE_EQUAL, smaller, larger);

    // Assert
    assert!(!result);
}

#[test]
fn less_with_smaller_left_returns_true() {
    // Arrange
    let smaller: isize = 50;
    let larger: isize = 100;

    // Act
    let result = run_comparison_op(opcode::LESS, smaller, larger);

    // Assert
    assert!(result);
}

#[test]
fn less_with_larger_left_returns_false() {
    // Arrange
    let larger: isize = 100;
    let smaller: isize = 50;

    // Act
    let result = run_comparison_op(opcode::LESS, larger, smaller);

    // Assert
    assert!(!result);
}

#[test]
fn less_with_equal_values_returns_false() {
    // Arrange
    let value: isize = 42;

    // Act
    let result = run_comparison_op(opcode::LESS, value, value);

    // Assert
    assert!(!result);
}

#[test]
fn less_with_negative_values_compares_correctly() {
    // Arrange
    let more_negative: isize = -10;
    let less_negative: isize = -5;

    // Act
    let result = run_comparison_op(opcode::LESS, more_negative, less_negative);

    // Assert
    assert!(result);
}

#[test]
fn less_equal_with_smaller_left_returns_true() {
    // Arrange
    let smaller: isize = 50;
    let larger: isize = 100;

    // Act
    let result = run_comparison_op(opcode::LESS_EQUAL, smaller, larger);

    // Assert
    assert!(result);
}

#[test]
fn less_equal_with_equal_values_returns_true() {
    // Arrange
    let value: isize = 42;

    // Act
    let result = run_comparison_op(opcode::LESS_EQUAL, value, value);

    // Assert
    assert!(result);
}

#[test]
fn less_equal_with_larger_left_returns_false() {
    // Arrange
    let larger: isize = 100;
    let smaller: isize = 50;

    // Act
    let result = run_comparison_op(opcode::LESS_EQUAL, larger, smaller);

    // Assert
    assert!(!result);
}
