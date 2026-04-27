use lighthouse::chunk::Chunk;
use lighthouse::value::Value;
use lighthouse::vm::VirtualMachine;
use lighthouse::opcode;

fn main() {
    let mut vm: VirtualMachine = VirtualMachine::new();

    // Add chunk
    let mut add_chunk: Chunk = Chunk::new("add", 3);
    let index = add_chunk.push_constant(Value::new_float(8.3));
    add_chunk
        .push_instruction(opcode::CONSTANT)
        .with_arguments(&[index, 0]);

    let index: usize = add_chunk.push_constant(Value::new_float(0.12));
    add_chunk
        .push_instruction(opcode::CONSTANT)
        .with_arguments(&[index, 1]);

    add_chunk
        .push_instruction(opcode::ADD_FLOAT)
        .with_arguments(&[0, 1, 2]);

    add_chunk
        .push_instruction(opcode::PRINT)
        .with_arguments(&[2]);

    add_chunk.push_instruction(opcode::RETURN);

    let add_chunk_index: usize = vm.push_chunk(add_chunk);

    // print chunk
    let mut print_chunk: Chunk = Chunk::new("print", 2);

    let index: usize = print_chunk.push_constant(Value::new_bool(true));
    print_chunk
        .push_instruction(opcode::CONSTANT)
        .with_arguments(&[index, 0]);

    let index: usize = print_chunk.push_constant(Value::new_bool(false));
    print_chunk
        .push_instruction(opcode::CONSTANT)
        .with_arguments(&[index, 1]);

    print_chunk
        .push_instruction(opcode::PRINT)
        .with_arguments(&[0]);

    print_chunk
        .push_instruction(opcode::PRINT)
        .with_arguments(&[1]);

    print_chunk.push_instruction(opcode::RETURN);

    let print_chunk_index: usize = vm.push_chunk(print_chunk);

    // main chunk
    let mut main_chunk: Chunk = Chunk::new("main", 0);

    main_chunk
        .push_instruction(opcode::CALL)
        .with_arguments(&[add_chunk_index]);

    main_chunk
        .push_instruction(opcode::CALL)
        .with_arguments(&[print_chunk_index]);

    main_chunk.push_instruction(opcode::RETURN);

    vm.run(main_chunk);
}
