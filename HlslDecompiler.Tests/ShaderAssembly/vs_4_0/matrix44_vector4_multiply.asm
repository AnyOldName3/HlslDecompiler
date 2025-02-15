//
// Generated by Microsoft (R) HLSL Shader Compiler 6.3.9600.16384
//
//
// Buffer Definitions: 
//
// cbuffer $Globals
// {
//
//   float4x4 matrix_4x4;               // Offset:    0 Size:    64
//
// }
//
//
// Resource Bindings:
//
// Name                                 Type  Format         Dim Slot Elements
// ------------------------------ ---------- ------- ----------- ---- --------
// $Globals                          cbuffer      NA          NA    0        1
//
//
//
// Input signature:
//
// Name                 Index   Mask Register SysValue  Format   Used
// -------------------- ----- ------ -------- -------- ------- ------
// POSITION                 0   xyzw        0     NONE   float   xyzw
//
//
// Output signature:
//
// Name                 Index   Mask Register SysValue  Format   Used
// -------------------- ----- ------ -------- -------- ------- ------
// POSITION                 0   xyzw        0     NONE   float   xyzw
// POSITION                 1   xyzw        1     NONE   float   xyzw
// POSITION                 2   xyzw        2     NONE   float   xyzw
// POSITION                 3   xyzw        3     NONE   float   xyzw
//
vs_4_0
dcl_constantbuffer cb0[4], immediateIndexed
dcl_input v0.xyzw
dcl_output o0.xyzw
dcl_output o1.xyzw
dcl_output o2.xyzw
dcl_output o3.xyzw
dcl_temps 2
mul r0.xyzw, v0.yyyy, cb0[1].xyzw
mad r0.xyzw, cb0[0].xyzw, v0.xxxx, r0.xyzw
mad r0.xyzw, cb0[2].xyzw, v0.zzzz, r0.xyzw
mad o0.xyzw, cb0[3].xyzw, v0.wwww, r0.xyzw
mul r0.xyzw, v0.xxxx, cb0[1].xyzw
mad r0.xyzw, cb0[0].xyzw, v0.yyyy, r0.xyzw
mad r0.xyzw, cb0[2].xyzw, v0.zzzz, r0.xyzw
mad o1.xyzw, cb0[3].xyzw, v0.wwww, r0.xyzw
mul r0.xyzw, |v0.xxxx|, cb0[1].xyzw
mad r0.xyzw, cb0[0].xyzw, |v0.yyyy|, r0.xyzw
mad r0.xyzw, cb0[2].xyzw, |v0.zzzz|, r0.xyzw
mad o2.xyzw, cb0[3].xyzw, |v0.wwww|, r0.xyzw
mul r0.xyzw, v0.xyzw, l(5.000000, 2.000000, 3.000000, 4.000000)
mul r1.xyzw, r0.yyyy, cb0[1].xyzw
mad r1.xyzw, cb0[0].xyzw, r0.xxxx, r1.xyzw
mad r1.xyzw, cb0[2].xyzw, r0.zzzz, r1.xyzw
mad o3.xyzw, cb0[3].xyzw, r0.wwww, r1.xyzw
ret 
// Approximately 18 instruction slots used
