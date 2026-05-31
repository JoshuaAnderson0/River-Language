use crate::chunk::Chunk;
use crate::opcode;
use crate::value::Value;
use crate::vm::VirtualMachine;

fn run_int_binop(op: u8, left: isize, right: isize) -> isize {
    let mut chunk = Chunk::new("test", 3);

    let c0 = chunk.push_constant(Value::new_int(left));
    let c1 = chunk.push_constant(Value::new_int(right));

    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c0, 0]);
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c1, 1]);
    chunk.push_instruction(op).with_arguments(&[0, 1, 2]);
    chunk.push_instruction(opcode::RETURN);

    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    vm.get_register(2)._value
}

#[test]
fn add_int_with_positive_operands_returns_sum() {
    // Arrange
    let left = 10;
    let right = 20;

    // Act
    let result = run_int_binop(opcode::ADD_INT, left, right);

    // Assert
    assert_eq!(result, 30);
}

#[test]
fn add_int_with_negative_operands_returns_sum() {
    // Arrange
    let left = -10;
    let right = -20;

    // Act
    let result = run_int_binop(opcode::ADD_INT, left, right);

    // Assert
    assert_eq!(result, -30);
}

#[test]
fn add_int_with_mixed_operands_returns_sum() {
    // Arrange
    let left = 10;
    let right = -20;

    // Act
    let result = run_int_binop(opcode::ADD_INT, left, right);

    // Assert
    assert_eq!(result, -10);
}

#[test]
fn add_int_with_zero_operands_returns_zero() {
    // Arrange
    let left = 0;
    let right = 0;

    // Act
    let result = run_int_binop(opcode::ADD_INT, left, right);

    // Assert
    assert_eq!(result, 0);
}

#[test]
fn add_int_on_overflow_wraps_to_min() {
    // Arrange
    let left = isize::MAX;
    let right = 1;

    // Act
    let result = run_int_binop(opcode::ADD_INT, left, right);

    // Assert
    assert_eq!(result, isize::MIN);
}

#[test]
fn sub_int_with_positive_operands_returns_difference() {
    // Arrange
    let left = 30;
    let right = 10;

    // Act
    let result = run_int_binop(opcode::SUB_INT, left, right);

    // Assert
    assert_eq!(result, 20);
}

#[test]
fn sub_int_with_smaller_left_returns_negative() {
    // Arrange
    let left = 10;
    let right = 30;

    // Act
    let result = run_int_binop(opcode::SUB_INT, left, right);

    // Assert
    assert_eq!(result, -20);
}

#[test]
fn sub_int_with_negative_operands_returns_difference() {
    // Arrange
    let left = -10;
    let right = -30;

    // Act
    let result = run_int_binop(opcode::SUB_INT, left, right);

    // Assert
    assert_eq!(result, 20);
}

#[test]
fn sub_int_with_zero_operands_returns_zero() {
    // Arrange
    let left = 0;
    let right = 0;

    // Act
    let result = run_int_binop(opcode::SUB_INT, left, right);

    // Assert
    assert_eq!(result, 0);
}

#[test]
fn sub_int_on_underflow_wraps_to_max() {
    // Arrange
    let left = isize::MIN;
    let right = 1;

    // Act
    let result = run_int_binop(opcode::SUB_INT, left, right);

    // Assert
    assert_eq!(result, isize::MAX);
}

#[test]
fn mul_int_with_positive_operands_returns_product() {
    // Arrange
    let left = 6;
    let right = 7;

    // Act
    let result = run_int_binop(opcode::MUL_INT, left, right);

    // Assert
    assert_eq!(result, 42);
}

#[test]
fn mul_int_with_one_negative_returns_negative() {
    // Arrange
    let left = -6;
    let right = 7;

    // Act
    let result = run_int_binop(opcode::MUL_INT, left, right);

    // Assert
    assert_eq!(result, -42);
}

#[test]
fn mul_int_with_both_negative_returns_positive() {
    // Arrange
    let left = -6;
    let right = -7;

    // Act
    let result = run_int_binop(opcode::MUL_INT, left, right);

    // Assert
    assert_eq!(result, 42);
}

#[test]
fn mul_int_with_zero_returns_zero() {
    // Arrange
    let left = 100;
    let right = 0;

    // Act
    let result = run_int_binop(opcode::MUL_INT, left, right);

    // Assert
    assert_eq!(result, 0);
}

#[test]
fn mul_int_with_one_returns_same() {
    // Arrange
    let left = 42;
    let right = 1;

    // Act
    let result = run_int_binop(opcode::MUL_INT, left, right);

    // Assert
    assert_eq!(result, 42);
}

#[test]
fn div_int_with_positive_operands_returns_quotient() {
    // Arrange
    let left = 42;
    let right = 7;

    // Act
    let result = run_int_binop(opcode::DIV_INT, left, right);

    // Assert
    assert_eq!(result, 6);
}

#[test]
fn div_int_with_negative_dividend_returns_negative() {
    // Arrange
    let left = -42;
    let right = 7;

    // Act
    let result = run_int_binop(opcode::DIV_INT, left, right);

    // Assert
    assert_eq!(result, -6);
}

#[test]
fn div_int_with_both_negative_returns_positive() {
    // Arrange
    let left = -42;
    let right = -7;

    // Act
    let result = run_int_binop(opcode::DIV_INT, left, right);

    // Assert
    assert_eq!(result, 6);
}

#[test]
fn div_int_with_remainder_truncates() {
    // Arrange
    let left = 10;
    let right = 3;

    // Act
    let result = run_int_binop(opcode::DIV_INT, left, right);

    // Assert
    assert_eq!(result, 3);
}

#[test]
fn div_int_by_one_returns_same() {
    // Arrange
    let left = 42;
    let right = 1;

    // Act
    let result = run_int_binop(opcode::DIV_INT, left, right);

    // Assert
    assert_eq!(result, 42);
}

#[test]
#[should_panic]
fn div_int_by_zero_panics() {
    // Arrange
    let left = 42;
    let right = 0;

    // Act
    run_int_binop(opcode::DIV_INT, left, right);

    // Assert
}
