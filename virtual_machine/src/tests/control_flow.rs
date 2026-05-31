use crate::chunk::Chunk;
use crate::opcode;
use crate::value::Value;
use crate::vm::VirtualMachine;

#[test]
fn return_after_constant_stops_execution() {
    // Arrange
    let mut chunk = Chunk::new("test", 1);
    let expected_const = chunk.push_constant(Value::new_int(42));
    let unreachable_const = chunk.push_constant(Value::new_int(0));
    let dest_reg = 0;
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[expected_const, dest_reg]);
    chunk.push_instruction(opcode::RETURN);
    chunk.push_instruction(opcode::CONSTANT).with_arguments(&[unreachable_const, dest_reg]);

    // Act
    let mut vm = VirtualMachine::new();
    vm.run(chunk);

    // Assert
    assert_eq!(vm.get_register(dest_reg)._value, 42);
}

#[test]
fn call_to_chunk_executes_callee() {
    // Arrange
    let mut vm = VirtualMachine::new();

    let mut callee = Chunk::new("callee", 1);
    let callee_const = callee.push_constant(Value::new_int(999));
    let dest_reg = 0;
    callee.push_instruction(opcode::CONSTANT).with_arguments(&[callee_const, dest_reg]);
    callee.push_instruction(opcode::RETURN);

    let callee_idx = vm.push_chunk(callee);

    let mut caller = Chunk::new("caller", 1);
    caller.push_instruction(opcode::CALL).with_arguments(&[callee_idx]);
    caller.push_instruction(opcode::RETURN);

    // Act
    vm.run(caller);

    // Assert
}

#[test]
fn call_after_return_continues_caller() {
    // Arrange
    let mut vm = VirtualMachine::new();
    let dest_reg = 0;

    let mut callee = Chunk::new("callee", 1);
    let callee_const = callee.push_constant(Value::new_int(100));
    callee.push_instruction(opcode::CONSTANT).with_arguments(&[callee_const, dest_reg]);
    callee.push_instruction(opcode::RETURN);

    let callee_idx = vm.push_chunk(callee);

    let mut caller = Chunk::new("caller", 1);
    let caller_const = caller.push_constant(Value::new_int(200));
    caller.push_instruction(opcode::CALL).with_arguments(&[callee_idx]);
    caller.push_instruction(opcode::CONSTANT).with_arguments(&[caller_const, dest_reg]);
    caller.push_instruction(opcode::RETURN);

    // Act
    vm.run(caller);

    // Assert
    assert_eq!(vm.get_register(dest_reg)._value, 200);
}

#[test]
fn call_with_nested_calls_unwinds_correctly() {
    // Arrange
    let mut vm = VirtualMachine::new();
    let dest_reg = 0;

    let mut inner = Chunk::new("inner", 1);
    let inner_const = inner.push_constant(Value::new_int(1));
    inner.push_instruction(opcode::CONSTANT).with_arguments(&[inner_const, dest_reg]);
    inner.push_instruction(opcode::RETURN);

    let inner_idx = vm.push_chunk(inner);

    let mut middle = Chunk::new("middle", 1);
    middle.push_instruction(opcode::CALL).with_arguments(&[inner_idx]);
    middle.push_instruction(opcode::RETURN);

    let middle_idx = vm.push_chunk(middle);

    let mut outer = Chunk::new("outer", 1);
    outer.push_instruction(opcode::CALL).with_arguments(&[middle_idx]);
    outer.push_instruction(opcode::RETURN);

    // Act
    vm.run(outer);

    // Assert
}

#[test]
fn call_multiple_sequential_executes_all() {
    // Arrange
    let mut vm = VirtualMachine::new();
    let dest_reg = 0;

    let mut func_a = Chunk::new("func_a", 1);
    let func_a_const = func_a.push_constant(Value::new_int(10));
    func_a.push_instruction(opcode::CONSTANT).with_arguments(&[func_a_const, dest_reg]);
    func_a.push_instruction(opcode::RETURN);

    let func_a_idx = vm.push_chunk(func_a);

    let mut func_b = Chunk::new("func_b", 1);
    let func_b_const = func_b.push_constant(Value::new_int(20));
    func_b.push_instruction(opcode::CONSTANT).with_arguments(&[func_b_const, dest_reg]);
    func_b.push_instruction(opcode::RETURN);

    let func_b_idx = vm.push_chunk(func_b);

    let mut main = Chunk::new("main", 1);
    let main_const = main.push_constant(Value::new_int(30));
    main.push_instruction(opcode::CALL).with_arguments(&[func_a_idx]);
    main.push_instruction(opcode::CALL).with_arguments(&[func_b_idx]);
    main.push_instruction(opcode::CONSTANT).with_arguments(&[main_const, dest_reg]);
    main.push_instruction(opcode::RETURN);

    // Act
    vm.run(main);

    // Assert
    assert_eq!(vm.get_register(dest_reg)._value, 30);
}

#[test]
fn call_to_chunk_isolates_registers() {
    // Arrange
    let mut vm = VirtualMachine::new();
    let reg_a = 0;
    let reg_b = 1;

    let mut callee = Chunk::new("callee", 2);
    let callee_const = callee.push_constant(Value::new_int(999));
    callee.push_instruction(opcode::CONSTANT).with_arguments(&[callee_const, reg_a]);
    callee.push_instruction(opcode::CONSTANT).with_arguments(&[callee_const, reg_b]);
    callee.push_instruction(opcode::RETURN);

    let callee_idx = vm.push_chunk(callee);

    let mut caller = Chunk::new("caller", 2);
    let caller_const_a = caller.push_constant(Value::new_int(111));
    let caller_const_b = caller.push_constant(Value::new_int(222));
    caller.push_instruction(opcode::CONSTANT).with_arguments(&[caller_const_a, reg_a]);
    caller.push_instruction(opcode::CONSTANT).with_arguments(&[caller_const_b, reg_b]);
    caller.push_instruction(opcode::CALL).with_arguments(&[callee_idx]);
    caller.push_instruction(opcode::RETURN);

    // Act
    vm.run(caller);

    // Assert
    assert_eq!(vm.get_register(reg_a)._value, 111);
    assert_eq!(vm.get_register(reg_b)._value, 222);
}

#[test]
fn call_with_deep_stack_unwinds_all() {
    // Arrange
    let mut vm = VirtualMachine::new();
    let depth = 9;
    let dest_reg = 0;

    for i in 0..depth {
        let mut chunk = Chunk::new("deep", 1);
        let chunk_const = chunk.push_constant(Value::new_int(i as isize));
        chunk.push_instruction(opcode::CONSTANT).with_arguments(&[chunk_const, dest_reg]);
        if i < depth - 1 {
            let next_idx = i + 1;
            chunk.push_instruction(opcode::CALL).with_arguments(&[next_idx]);
        }
        chunk.push_instruction(opcode::RETURN);
        vm.push_chunk(chunk);
    }

    let first_chunk_idx = 0;
    let mut entry = Chunk::new("entry", 1);
    entry.push_instruction(opcode::CALL).with_arguments(&[first_chunk_idx]);
    entry.push_instruction(opcode::RETURN);

    // Act
    vm.run(entry);

    // Assert
}
