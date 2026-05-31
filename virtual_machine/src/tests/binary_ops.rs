use crate::chunk::Chunk;
use crate::opcode;
use crate::value::Value;
use crate::vm::VirtualMachine;

const LEFT_REG: usize = 0;
const RIGHT_REG: usize = 1;
const RESULT_REG: usize = 2;

fn run_binary_op(op: u8, left_value: isize, right_value: isize) -> isize {
    let mut chunk = Chunk::new("test", 3);

    let left_const = chunk.push_constant(Value::new_int(left_value));
    let right_const = chunk.push_constant(Value::new_int(right_value));

    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[left_const, LEFT_REG]);
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[right_const, RIGHT_REG]);
    chunk.push_instruction(op).with_arguments(&[LEFT_REG, RIGHT_REG, RESULT_REG]);
    chunk.push_instruction(opcode::RETURN);

    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    vm.get_register(RESULT_REG)._value
}

#[test]
fn binary_and_with_all_ones_returns_same() {
    // Arrange
    let all_ones: isize = 0b1111_1111;

    // Act
    let result = run_binary_op(opcode::BINARY_AND, all_ones, all_ones);

    // Assert
    assert_eq!(result, all_ones);
}

#[test]
fn binary_and_with_zero_returns_zero() {
    // Arrange
    let all_ones: isize = 0b1111_1111;
    let zero: isize = 0;

    // Act
    let result = run_binary_op(opcode::BINARY_AND, all_ones, zero);

    // Assert
    assert_eq!(result, 0);
}

#[test]
fn binary_and_masks_bits() {
    // Arrange
    let alternating: isize = 0b1010_1010;
    let high_nibble_mask: isize = 0b1111_0000;
    let expected: isize = 0b1010_0000;

    // Act
    let result = run_binary_op(opcode::BINARY_AND, alternating, high_nibble_mask);

    // Assert
    assert_eq!(result, expected);
}

#[test]
fn binary_and_with_non_overlapping_returns_zero() {
    // Arrange
    let even_bits: isize = 0b1010_1010;
    let odd_bits: isize = 0b0101_0101;

    // Act
    let result = run_binary_op(opcode::BINARY_AND, even_bits, odd_bits);

    // Assert
    assert_eq!(result, 0);
}

#[test]
fn binary_or_with_zero_returns_same() {
    // Arrange
    let alternating: isize = 0b1010_1010;
    let zero: isize = 0;

    // Act
    let result = run_binary_op(opcode::BINARY_OR, alternating, zero);

    // Assert
    assert_eq!(result, alternating);
}

#[test]
fn binary_or_combines_disjoint_bits() {
    // Arrange
    let high_nibble: isize = 0b1010_0000;
    let low_nibble: isize = 0b0000_1010;
    let expected: isize = 0b1010_1010;

    // Act
    let result = run_binary_op(opcode::BINARY_OR, high_nibble, low_nibble);

    // Assert
    assert_eq!(result, expected);
}

#[test]
fn binary_or_with_complementary_bits_returns_all_ones() {
    // Arrange
    let high_nibble: isize = 0b1111_0000;
    let low_nibble: isize = 0b0000_1111;
    let all_ones: isize = 0b1111_1111;

    // Act
    let result = run_binary_op(opcode::BINARY_OR, high_nibble, low_nibble);

    // Assert
    assert_eq!(result, all_ones);
}

#[test]
fn binary_or_with_same_value_returns_same() {
    // Arrange
    let alternating: isize = 0b1010_1010;

    // Act
    let result = run_binary_op(opcode::BINARY_OR, alternating, alternating);

    // Assert
    assert_eq!(result, alternating);
}

#[test]
fn binary_xor_with_same_value_returns_zero() {
    // Arrange
    let alternating: isize = 0b1010_1010;

    // Act
    let result = run_binary_op(opcode::BINARY_XOR, alternating, alternating);

    // Assert
    assert_eq!(result, 0);
}

#[test]
fn binary_xor_with_zero_returns_same() {
    // Arrange
    let alternating: isize = 0b1010_1010;
    let zero: isize = 0;

    // Act
    let result = run_binary_op(opcode::BINARY_XOR, alternating, zero);

    // Assert
    assert_eq!(result, alternating);
}

#[test]
fn binary_xor_flips_overlapping_bits() {
    // Arrange
    let high_nibble: isize = 0b1111_0000;
    let alternating: isize = 0b1010_1010;
    let expected: isize = 0b0101_1010;

    // Act
    let result = run_binary_op(opcode::BINARY_XOR, high_nibble, alternating);

    // Assert
    assert_eq!(result, expected);
}

#[test]
fn binary_xor_with_all_ones_inverts_byte() {
    // Arrange
    let alternating: isize = 0b1010_1010;
    let all_ones: isize = 0b1111_1111;
    let inverted: isize = 0b0101_0101;

    // Act
    let result = run_binary_op(opcode::BINARY_XOR, alternating, all_ones);

    // Assert
    assert_eq!(result, inverted);
}

#[test]
fn binary_lshift_by_zero_returns_same() {
    // Arrange
    let alternating: isize = 0b1010_1010;
    let shift_amount: isize = 0;

    // Act
    let result = run_binary_op(opcode::BINARY_LSHIFT, alternating, shift_amount);

    // Assert
    assert_eq!(result, alternating);
}

#[test]
fn binary_lshift_by_one_doubles_value() {
    // Arrange
    let five: isize = 5;
    let shift_one: isize = 1;
    let doubled: isize = 10;

    // Act
    let result = run_binary_op(opcode::BINARY_LSHIFT, five, shift_one);

    // Assert
    assert_eq!(result, doubled);
}

#[test]
fn binary_lshift_by_four_multiplies_by_sixteen() {
    // Arrange
    let one: isize = 1;
    let shift_four: isize = 4;
    let sixteen: isize = 16;

    // Act
    let result = run_binary_op(opcode::BINARY_LSHIFT, one, shift_four);

    // Assert
    assert_eq!(result, sixteen);
}

#[test]
fn binary_lshift_moves_bits_left() {
    // Arrange
    let low_nibble: isize = 0b0000_1111;
    let shift_four: isize = 4;
    let high_nibble: isize = 0b1111_0000;

    // Act
    let result = run_binary_op(opcode::BINARY_LSHIFT, low_nibble, shift_four);

    // Assert
    assert_eq!(result, high_nibble);
}

#[test]
fn binary_rshift_by_zero_returns_same() {
    // Arrange
    let alternating: isize = 0b1010_1010;
    let shift_amount: isize = 0;

    // Act
    let result = run_binary_op(opcode::BINARY_RSHIFT, alternating, shift_amount);

    // Assert
    assert_eq!(result, alternating);
}

#[test]
fn binary_rshift_by_one_halves_value() {
    // Arrange
    let ten: isize = 10;
    let shift_one: isize = 1;
    let halved: isize = 5;

    // Act
    let result = run_binary_op(opcode::BINARY_RSHIFT, ten, shift_one);

    // Assert
    assert_eq!(result, halved);
}

#[test]
fn binary_rshift_by_four_divides_by_sixteen() {
    // Arrange
    let sixteen: isize = 16;
    let shift_four: isize = 4;
    let one: isize = 1;

    // Act
    let result = run_binary_op(opcode::BINARY_RSHIFT, sixteen, shift_four);

    // Assert
    assert_eq!(result, one);
}

#[test]
fn binary_rshift_moves_bits_right() {
    // Arrange
    let high_nibble: isize = 0b1111_0000;
    let shift_four: isize = 4;
    let low_nibble: isize = 0b0000_1111;

    // Act
    let result = run_binary_op(opcode::BINARY_RSHIFT, high_nibble, shift_four);

    // Assert
    assert_eq!(result, low_nibble);
}

#[test]
fn binary_rshift_discards_low_bits() {
    // Arrange
    let all_ones_byte: isize = 0b1111_1111;
    let shift_four: isize = 4;
    let low_nibble: isize = 0b0000_1111;

    // Act
    let result = run_binary_op(opcode::BINARY_RSHIFT, all_ones_byte, shift_four);

    // Assert
    assert_eq!(result, low_nibble);
}
