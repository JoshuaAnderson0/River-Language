use crate::chunk::Chunk;
use crate::opcode;
use crate::value::Value;
use crate::vm::VirtualMachine;

fn run_float_binop(op: u8, left: f64, right: f64) -> f64 {
    let mut chunk = Chunk::new("test", 3);

    let c0 = chunk.push_constant(Value::new_float(left));
    let c1 = chunk.push_constant(Value::new_float(right));

    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c0, 0]);
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[c1, 1]);
    chunk.push_instruction(op).with_arguments(&[0, 1, 2]);
    chunk.push_instruction(opcode::RETURN);

    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    let result = vm.get_register(2)._value;
    f64::from_bits(result as u64)
}

fn approx_eq(a: f64, b: f64) -> bool {
    (a - b).abs() < 1e-10
}

#[test]
fn add_float_with_positive_operands_returns_sum() {
    // Arrange
    let left = 8.3;
    let right = 0.12;

    // Act
    let result = run_float_binop(opcode::ADD_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 8.42), "expected 8.42, got {}", result);
}

#[test]
fn add_float_with_negative_operands_returns_sum() {
    // Arrange
    let left = -1.5;
    let right = -2.5;

    // Act
    let result = run_float_binop(opcode::ADD_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, -4.0), "expected -4.0, got {}", result);
}

#[test]
fn add_float_with_mixed_operands_returns_sum() {
    // Arrange
    let left = 10.0;
    let right = -3.5;

    // Act
    let result = run_float_binop(opcode::ADD_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 6.5), "expected 6.5, got {}", result);
}

#[test]
fn add_float_with_zero_operands_returns_zero() {
    // Arrange
    let left = 0.0;
    let right = 0.0;

    // Act
    let result = run_float_binop(opcode::ADD_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 0.0), "expected 0.0, got {}", result);
}

#[test]
fn add_float_with_small_operands_returns_sum() {
    // Arrange
    let left = 0.001;
    let right = 0.002;

    // Act
    let result = run_float_binop(opcode::ADD_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 0.003), "expected 0.003, got {}", result);
}

#[test]
fn sub_float_with_positive_operands_returns_difference() {
    // Arrange
    let left = 10.5;
    let right = 3.2;

    // Act
    let result = run_float_binop(opcode::SUB_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 7.3), "expected 7.3, got {}", result);
}

#[test]
fn sub_float_with_smaller_left_returns_negative() {
    // Arrange
    let left = 3.0;
    let right = 5.0;

    // Act
    let result = run_float_binop(opcode::SUB_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, -2.0), "expected -2.0, got {}", result);
}

#[test]
fn sub_float_with_equal_operands_returns_zero() {
    // Arrange
    let left = 5.5;
    let right = 5.5;

    // Act
    let result = run_float_binop(opcode::SUB_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 0.0), "expected 0.0, got {}", result);
}

#[test]
fn mul_float_with_positive_operands_returns_product() {
    // Arrange
    let left = 3.0;
    let right = 4.0;

    // Act
    let result = run_float_binop(opcode::MUL_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 12.0), "expected 12.0, got {}", result);
}

#[test]
fn mul_float_with_one_negative_returns_negative() {
    // Arrange
    let left = -2.5;
    let right = 4.0;

    // Act
    let result = run_float_binop(opcode::MUL_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, -10.0), "expected -10.0, got {}", result);
}

#[test]
fn mul_float_with_both_negative_returns_positive() {
    // Arrange
    let left = -2.0;
    let right = -3.0;

    // Act
    let result = run_float_binop(opcode::MUL_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 6.0), "expected 6.0, got {}", result);
}

#[test]
fn mul_float_with_zero_returns_zero() {
    // Arrange
    let left = 100.0;
    let right = 0.0;

    // Act
    let result = run_float_binop(opcode::MUL_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 0.0), "expected 0.0, got {}", result);
}

#[test]
fn mul_float_with_one_returns_same() {
    // Arrange
    let left = 42.5;
    let right = 1.0;

    // Act
    let result = run_float_binop(opcode::MUL_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 42.5), "expected 42.5, got {}", result);
}

#[test]
fn mul_float_with_fractions_returns_product() {
    // Arrange
    let left = 0.5;
    let right = 0.5;

    // Act
    let result = run_float_binop(opcode::MUL_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 0.25), "expected 0.25, got {}", result);
}

#[test]
fn div_float_with_positive_operands_returns_quotient() {
    // Arrange
    let left = 10.0;
    let right = 2.0;

    // Act
    let result = run_float_binop(opcode::DIV_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 5.0), "expected 5.0, got {}", result);
}

#[test]
fn div_float_with_negative_dividend_returns_negative() {
    // Arrange
    let left = -10.0;
    let right = 2.0;

    // Act
    let result = run_float_binop(opcode::DIV_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, -5.0), "expected -5.0, got {}", result);
}

#[test]
fn div_float_with_non_integer_result_returns_fraction() {
    // Arrange
    let left = 1.0;
    let right = 3.0;

    // Act
    let result = run_float_binop(opcode::DIV_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 1.0 / 3.0), "expected 0.333..., got {}", result);
}

#[test]
fn div_float_by_one_returns_same() {
    // Arrange
    let left = 42.5;
    let right = 1.0;

    // Act
    let result = run_float_binop(opcode::DIV_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 42.5), "expected 42.5, got {}", result);
}

#[test]
fn div_float_with_equal_operands_returns_one() {
    // Arrange
    let left = 7.7;
    let right = 7.7;

    // Act
    let result = run_float_binop(opcode::DIV_FLOAT, left, right);

    // Assert
    assert!(approx_eq(result, 1.0), "expected 1.0, got {}", result);
}

#[test]
fn div_float_by_zero_returns_infinity() {
    // Arrange
    let left = 1.0;
    let right = 0.0;

    // Act
    let result = run_float_binop(opcode::DIV_FLOAT, left, right);

    // Assert
    assert!(result.is_infinite() && result > 0.0, "expected +inf, got {}", result);
}

#[test]
fn div_float_zero_by_zero_returns_nan() {
    // Arrange
    let left = 0.0;
    let right = 0.0;

    // Act
    let result = run_float_binop(opcode::DIV_FLOAT, left, right);

    // Assert
    assert!(result.is_nan(), "expected NaN, got {}", result);
}
