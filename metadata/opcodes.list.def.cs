/// <auto-generated> don't touch this file, for modification use gen.csx </auto-generated>
namespace wave 
{
	using global::wave.runtime.emit;
	public static class OpCodes 
	{
		public static OpCode NOP = new (0x00, 0x00001F);
		public static OpCode ADD = new (0x01, 0x00001F);
		public static OpCode SUB = new (0x02, 0x00001F);
		public static OpCode DIV = new (0x03, 0x00001F);
		public static OpCode MUL = new (0x04, 0x00001F);
		public static OpCode LDARG_0 = new (0x05, 0x00001F);
		public static OpCode LDARG_1 = new (0x06, 0x00001F);
		public static OpCode LDARG_2 = new (0x07, 0x00001F);
		public static OpCode LDARG_3 = new (0x08, 0x00001F);
		public static OpCode LDARG_4 = new (0x09, 0x00001F);
		public static OpCode LDC_I4_0 = new (0x0A, 0x00001F);
		public static OpCode LDC_I4_S = new (0x0B, 0x40001F);
		public static OpCode DUMP_0 = new (0x0C, 0x00001F);
		public static OpCode DUMP_1 = new (0x0D, 0x00001F);
		public static OpCode RET = new (0x0E, 0x00001F);
		public static OpCode CALL = new (0x0F, 0x80001F);
		public static OpCode LDNULL = new (0x10, 0x00001F);
		public static OpCode LDLOC_0 = new (0x11, 0x00001F);
		public static OpCode LDLOC_1 = new (0x12, 0x00001F);
		public static OpCode LDLOC_2 = new (0x13, 0x00001F);
		public static OpCode LDLOC_3 = new (0x14, 0x00001F);
		public static OpCode LDLOC_4 = new (0x15, 0x00001F);
		public static OpCode STLOC_0 = new (0x16, 0x00001F);
		public static OpCode STLOC_1 = new (0x17, 0x00001F);
		public static OpCode STLOC_2 = new (0x18, 0x00001F);
		public static OpCode STLOC_3 = new (0x19, 0x00001F);
		public static OpCode STLOC_4 = new (0x1A, 0x00001F);
		public static OpCode LOC_INIT = new (0x1B, 0x00001F);
		public static OpCode DUP = new (0x1C, 0x00001F);
		public static OpCode XOR = new (0x1D, 0x00001F);
		public static OpCode AND = new (0x1E, 0x00001F);
		public static OpCode SHR = new (0x1F, 0x00001F);
		public static OpCode SHL = new (0x20, 0x00001F);
		public static OpCode CONV_R4 = new (0x21, 0x00001F);
		public static OpCode CONV_R8 = new (0x22, 0x00001F);
		public static OpCode CONV_I4 = new (0x23, 0x00001F);
	}
}