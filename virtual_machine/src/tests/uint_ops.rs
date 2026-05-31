use crate::chunk::Chunk;
use crate::opcode;
use crate::value::Value;
use crate::vm::VirtualMachine;

fn run_uint_binop(op: u8, left: usize, right: usize) -> usize {
    let mut chunk = Chunk::new("test", 3);

    let c0 = chunk.push_constant(Value::new_uint(left));
    let c1 = chunk.push_constant(Value::new_uint(right));

    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c0, 0]);
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c1, 1]);
    chunk.push_instruction(op).with_arguments(&[0, 1, 2]);
    chunk.push_instruction(opcode::RETURN);

    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    vm.get_register(2)._value as usize
}

#[test]
fn add_uint_with_positive_operands_returns_sum() {
    // Arrange
    let left = 10;
    let right = 20;

    // Act
    let result = run_uint_binop(opcode::ADD_UINT, left, right);

    // Assert
    assert_eq!(result, 30);
}

#[test]
fn add_uint_with_zero_operands_returns_zero() {
    // Arrange
    let left = 0;
    let right = 0;

    // Act
    let result = run_uint_binop(opcode::ADD_UINT, left, right);

    // Assert
    assert_eq!(result, 0);
}

#[test]
fn add_uint_with_large_operands_returns_sum() {
    // Arrange
    let left = 1_000_000;
    let right = 2_000_000;

    // Act
    let result = run_uint_binop(opcode::ADD_UINT, left, right);

    // Assert
    assert_eq!(result, 3_000_000);
}

#[test]
fn add_uint_on_overflow_wraps_to_zero() {
    // Arrange
    let left = usize::MAX;
    let right = 1;

    // Act
    let result = run_uint_binop(opcode::ADD_UINT, left, right);

    // Assert
    assert_eq!(result, 0);
}

#[test]
fn sub_uint_with_positive_operands_returns_difference() {
    // Arrange
    let left = 30;
    let right = 10;

    // Act
    let result = run_uint_binop(opcode::SUB_UINT, left, right);

    // Assert
    assert_eq!(result, 20);
}

#[test]
fn sub_uint_with_zero_right_returns_left() {
    // Arrange
    let left = 10;
    let right = 0;

    // Act
    let result = run_uint_binop(opcode::SUB_UINT, left, right);

    // Assert
    assert_eq!(result, 10);
}

#[test]
fn sub_uint_with_equal_operands_returns_zero() {
    // Arrange
    let left = 100;
    let right = 100;

    // Act
    let result = run_uint_binop(opcode::SUB_UINT, left, right);

    // Assert
    assert_eq!(result, 0);
}

#[test]
fn sub_uint_on_underflow_wraps_to_max() {
    // Arrange
    let left = 0;
    let right = 1;

    // Act
    let result = run_uint_binop(opcode::SUB_UINT, left, right);

    // Assert
    assert_eq!(result, usize::MAX);
}

#[test]
fn mul_uint_with_positive_operands_returns_product() {
    // Arrange
    let left = 6;
    let right = 7;

    // Act
    let result = run_uint_binop(opcode::MUL_UINT, left, right);

    // Assert
    assert_eq!(result, 42);
}

#[test]
fn mul_uint_with_zero_returns_zero() {
    // Arrange
    let left = 100;
    let right = 0;

    // Act
    let result = run_uint_binop(opcode::MUL_UINT, left, right);

    // Assert
    assert_eq!(result, 0);
}

#[test]
fn mul_uint_with_one_returns_same() {
    // Arrange
    let left = 42;
    let right = 1;

    // Act
    let result = run_uint_binop(opcode::MUL_UINT, left, right);

    // Assert
    assert_eq!(result, 42);
}

#[test]
fn mul_uint_with_large_operands_returns_product() {
    // Arrange
    let left = 1000;
    let right = 1000;

    // Act
    let result = run_uint_binop(opcode::MUL_UINT, left, right);

    // Assert
    assert_eq!(result, 1_000_000);
}

#[test]
fn div_uint_with_positive_operands_returns_quotient() {
    // Arrange
    let left = 42;
    let right = 7;

    // Act
    let result = run_uint_binop(opcode::DIV_UINT, left, right);

    // Assert
    assert_eq!(result, 6);
}

#[test]
fn div_uint_with_remainder_truncates() {
    // Arrange
    let left = 10;
    let right = 3;

    // Act
    let result = run_uint_binop(opcode::DIV_UINT, left, right);

    // Assert
    assert_eq!(result, 3);
}

#[test]
fn div_uint_by_one_returns_same() {
    // Arrange
    let left = 42;
    let right = 1;

    // Act
    let result = run_uint_binop(opcode::DIV_UINT, left, right);

    // Assert
    assert_eq!(result, 42);
}

#[test]
fn div_uint_with_equal_operands_returns_one() {
    // Arrange
    let left = 100;
    let right = 100;

    // Act
    let result = run_uint_binop(opcode::DIV_UINT, left, right);

    // Assert
    assert_eq!(result, 1);
}

#[test]
#[should_panic]
fn div_uint_by_zero_panics() {
    // Arrange
    let left = 42;
    let right = 0;

    // Act
    run_uint_binop(opcode::DIV_UINT, left, right);

    // Assert
}
