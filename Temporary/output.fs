module SpiralExample.Main
let cuda_kernels = """
#include "cub/cub.cuh"

extern "C" {
    typedef float(*FunPointer0)(float, float);
    __global__ void method_16(float * var_0, long long int var_1, float * var_2);
    __global__ void method_18(float * var_0, float * var_1, long long int var_2, float * var_3, long long int var_4);
    __global__ void method_26(float var_0, float var_1, float * var_2, float * var_3, float * var_4, long long int var_5, float * var_6);
    __global__ void method_28(float * var_0, float * var_1, float * var_2, float * var_3, long long int var_4, float * var_5);
    __global__ void method_31(float * var_0, float * var_1, float * var_2);
    __device__ void method_17(float * var_0, long long int var_1, float * var_2, long long int var_3, long long int var_4);
    __device__ float method_19(float * var_0, float * var_1, long long int var_2, long long int var_3, float var_4, long long int var_5);
    __device__ float method_20(float var_0, float var_1);
    __device__ void method_27(float var_0, float var_1, float * var_2, float * var_3, float * var_4, long long int var_5, float * var_6, long long int var_7, long long int var_8);
    __device__ void method_29(float * var_0, float * var_1, float * var_2, float * var_3, long long int var_4, float * var_5, long long int var_6, long long int var_7);
    __device__ void method_32(float * var_0, float * var_1, float * var_2, long long int var_3);
    
    __global__ void method_16(float * var_0, long long int var_1, float * var_2) {
        long long int var_3 = threadIdx.x;
        long long int var_4 = threadIdx.y;
        long long int var_5 = threadIdx.z;
        long long int var_6 = blockIdx.x;
        long long int var_7 = blockIdx.y;
        long long int var_8 = blockIdx.z;
        long long int var_9 = gridDim.x;
        long long int var_10 = (var_6 * 128);
        long long int var_11 = (var_10 + var_3);
        long long int var_12 = (var_9 * 128);
        method_17(var_0, var_1, var_2, var_12, var_11);
    }
    __global__ void method_18(float * var_0, float * var_1, long long int var_2, float * var_3, long long int var_4) {
        long long int var_5 = threadIdx.x;
        long long int var_6 = threadIdx.y;
        long long int var_7 = threadIdx.z;
        long long int var_8 = blockIdx.x;
        long long int var_9 = blockIdx.y;
        long long int var_10 = blockIdx.z;
        long long int var_11 = gridDim.x;
        long long int var_12 = (var_8 * 128);
        long long int var_13 = (var_12 + var_5);
        long long int var_14 = (var_11 * 128);
        float var_15 = 0;
        float var_16 = method_19(var_0, var_1, var_2, var_14, var_15, var_13);
        FunPointer0 var_19 = method_20;
        float var_20 = cub::BlockReduce<float,128>().Reduce(var_16, var_19);
        char var_21 = (var_5 == 0);
        if (var_21) {
            char var_22 = (var_8 >= 0);
            char var_24;
            if (var_22) {
                var_24 = (var_8 < var_4);
            } else {
                var_24 = 0;
            }
            char var_25 = (var_24 == 0);
            if (var_25) {
                // unprinted assert;
            } else {
            }
            var_3[var_8] = var_20;
        } else {
        }
    }
    __global__ void method_26(float var_0, float var_1, float * var_2, float * var_3, float * var_4, long long int var_5, float * var_6) {
        long long int var_7 = threadIdx.x;
        long long int var_8 = threadIdx.y;
        long long int var_9 = threadIdx.z;
        long long int var_10 = blockIdx.x;
        long long int var_11 = blockIdx.y;
        long long int var_12 = blockIdx.z;
        long long int var_13 = gridDim.x;
        long long int var_14 = (var_10 * 128);
        long long int var_15 = (var_14 + var_7);
        long long int var_16 = (var_13 * 128);
        method_27(var_0, var_1, var_2, var_3, var_4, var_5, var_6, var_16, var_15);
    }
    __global__ void method_28(float * var_0, float * var_1, float * var_2, float * var_3, long long int var_4, float * var_5) {
        long long int var_6 = threadIdx.x;
        long long int var_7 = threadIdx.y;
        long long int var_8 = threadIdx.z;
        long long int var_9 = blockIdx.x;
        long long int var_10 = blockIdx.y;
        long long int var_11 = blockIdx.z;
        long long int var_12 = gridDim.x;
        long long int var_13 = (var_9 * 128);
        long long int var_14 = (var_13 + var_6);
        long long int var_15 = (var_12 * 128);
        method_29(var_0, var_1, var_2, var_3, var_4, var_5, var_15, var_14);
    }
    __global__ void method_31(float * var_0, float * var_1, float * var_2) {
        long long int var_3 = threadIdx.x;
        long long int var_4 = threadIdx.y;
        long long int var_5 = threadIdx.z;
        long long int var_6 = blockIdx.x;
        long long int var_7 = blockIdx.y;
        long long int var_8 = blockIdx.z;
        long long int var_9 = (var_6 * 128);
        long long int var_10 = (var_9 + var_3);
        method_32(var_0, var_1, var_2, var_10);
    }
    __device__ void method_17(float * var_0, long long int var_1, float * var_2, long long int var_3, long long int var_4) {
        char var_5 = (var_4 < var_1);
        if (var_5) {
            char var_6 = (var_4 >= 0);
            char var_7 = (var_6 == 0);
            if (var_7) {
                // unprinted assert;
            } else {
            }
            if (var_7) {
                // unprinted assert;
            } else {
            }
            float var_8 = var_0[var_4];
            float var_9 = (-var_8);
            float var_10 = exp(var_9);
            float var_11 = (1 + var_10);
            float var_12 = (1 / var_11);
            var_2[var_4] = var_12;
            long long int var_13 = (var_4 + var_3);
            method_17(var_0, var_1, var_2, var_3, var_13);
        } else {
        }
    }
    __device__ float method_19(float * var_0, float * var_1, long long int var_2, long long int var_3, float var_4, long long int var_5) {
        char var_6 = (var_5 < var_2);
        if (var_6) {
            char var_7 = (var_5 >= 0);
            char var_8 = (var_7 == 0);
            if (var_8) {
                // unprinted assert;
            } else {
            }
            float var_9 = var_0[var_5];
            float var_10 = var_1[var_5];
            float var_11 = (var_10 - var_9);
            float var_12 = (var_11 * var_11);
            float var_13 = (var_4 + var_12);
            long long int var_14 = (var_5 + var_3);
            return method_19(var_0, var_1, var_2, var_3, var_13, var_14);
        } else {
            return var_4;
        }
    }
    __device__ float method_20(float var_0, float var_1) {
        return (var_0 + var_1);
    }
    __device__ void method_27(float var_0, float var_1, float * var_2, float * var_3, float * var_4, long long int var_5, float * var_6, long long int var_7, long long int var_8) {
        char var_9 = (var_8 < var_5);
        if (var_9) {
            char var_10 = (var_8 >= 0);
            char var_11 = (var_10 == 0);
            if (var_11) {
                // unprinted assert;
            } else {
            }
            if (var_11) {
                // unprinted assert;
            } else {
            }
            float var_12 = var_2[var_8];
            float var_13 = var_3[var_8];
            float var_14 = var_4[var_8];
            float var_15 = (var_13 - var_14);
            float var_16 = (2 * var_15);
            float var_17 = (var_0 * var_16);
            float var_18 = (var_12 + var_17);
            var_6[var_8] = var_18;
            long long int var_19 = (var_8 + var_7);
            method_27(var_0, var_1, var_2, var_3, var_4, var_5, var_6, var_7, var_19);
        } else {
        }
    }
    __device__ void method_29(float * var_0, float * var_1, float * var_2, float * var_3, long long int var_4, float * var_5, long long int var_6, long long int var_7) {
        char var_8 = (var_7 < var_4);
        if (var_8) {
            char var_9 = (var_7 >= 0);
            char var_10 = (var_9 == 0);
            if (var_10) {
                // unprinted assert;
            } else {
            }
            if (var_10) {
                // unprinted assert;
            } else {
            }
            float var_11 = var_0[var_7];
            float var_12 = var_1[var_7];
            float var_13 = var_2[var_7];
            float var_14 = var_3[var_7];
            float var_15 = (1 - var_14);
            float var_16 = (var_14 * var_15);
            float var_17 = (var_13 * var_16);
            float var_18 = (var_11 + var_17);
            var_5[var_7] = var_18;
            long long int var_19 = (var_7 + var_6);
            method_29(var_0, var_1, var_2, var_3, var_4, var_5, var_6, var_19);
        } else {
        }
    }
    __device__ void method_32(float * var_0, float * var_1, float * var_2, long long int var_3) {
        char var_4 = (var_3 < 7840);
        if (var_4) {
            char var_5 = (var_3 >= 0);
            char var_6 = (var_5 == 0);
            if (var_6) {
                // unprinted assert;
            } else {
            }
            if (var_6) {
                // unprinted assert;
            } else {
            }
            float var_7 = var_0[var_3];
            float var_8 = var_1[var_3];
            float var_9 = (0.01 * var_7);
            float var_10 = (var_8 - var_9);
            var_2[var_3] = var_10;
            long long int var_11 = (var_3 + 7936);
            method_32(var_0, var_1, var_2, var_11);
        } else {
        }
    }
}
"""

type Union0 =
    | Union0Case0 of Tuple1
    | Union0Case1
and Tuple1 =
    struct
    val mem_0: ManagedCuda.BasicTypes.CUdeviceptr
    new(arg_mem_0) = {mem_0 = arg_mem_0}
    end
and Env2 =
    struct
    val mem_0: Env6
    val mem_1: int64
    new(arg_mem_0, arg_mem_1) = {mem_0 = arg_mem_0; mem_1 = arg_mem_1}
    end
and Tuple3 =
    struct
    val mem_0: Tuple4
    val mem_1: (uint8 [])
    new(arg_mem_0, arg_mem_1) = {mem_0 = arg_mem_0; mem_1 = arg_mem_1}
    end
and Tuple4 =
    struct
    val mem_0: int64
    val mem_1: int64
    val mem_2: int64
    new(arg_mem_0, arg_mem_1, arg_mem_2) = {mem_0 = arg_mem_0; mem_1 = arg_mem_1; mem_2 = arg_mem_2}
    end
and Tuple5 =
    struct
    val mem_0: int64
    val mem_1: (uint8 [])
    new(arg_mem_0, arg_mem_1) = {mem_0 = arg_mem_0; mem_1 = arg_mem_1}
    end
and Env6 =
    struct
    val mem_0: (Union0 ref)
    new(arg_mem_0) = {mem_0 = arg_mem_0}
    end
and Union7 =
    | Union7Case0 of Tuple8
    | Union7Case1
and Tuple8 =
    struct
    val mem_0: float32
    new(arg_mem_0) = {mem_0 = arg_mem_0}
    end
let rec method_0 ((var_0: System.Diagnostics.DataReceivedEventArgs)): unit =
    let (var_1: string) = var_0.get_Data()
    let (var_2: string) = System.String.Format("{0}",var_1)
    System.Console.WriteLine(var_2)
and method_1((var_0: (Union0 ref))): ManagedCuda.BasicTypes.CUdeviceptr =
    let (var_1: Union0) = (!var_0)
    match var_1 with
    | Union0Case0(var_2) ->
        var_2.mem_0
    | Union0Case1 ->
        (failwith "A Cuda memory cell that has been disposed has been tried to be accessed.")
and method_2((var_0: string)): Tuple3 =
    let (var_1: System.IO.FileMode) = System.IO.FileMode.Open
    let (var_2: System.IO.FileAccess) = System.IO.FileAccess.Read
    let (var_3: System.IO.FileShare) = System.IO.FileShare.Read
    let (var_4: System.IO.FileStream) = System.IO.File.Open(var_0, var_1, var_2, var_3)
    let (var_5: System.IO.BinaryReader) = System.IO.BinaryReader(var_4)
    let (var_6: int32) = var_5.ReadInt32()
    let (var_7: int32) = System.Net.IPAddress.NetworkToHostOrder(var_6)
    let (var_8: bool) = (var_7 = 2051)
    let (var_9: bool) = (var_8 = false)
    if var_9 then
        (failwith "Expected a 2051i32 magic number.")
    else
        ()
    let (var_10: int32) = var_5.ReadInt32()
    let (var_11: int32) = System.Net.IPAddress.NetworkToHostOrder(var_10)
    let (var_12: int32) = var_5.ReadInt32()
    let (var_13: int32) = System.Net.IPAddress.NetworkToHostOrder(var_12)
    let (var_14: int32) = var_5.ReadInt32()
    let (var_15: int32) = System.Net.IPAddress.NetworkToHostOrder(var_14)
    let (var_16: int64) = (int64 var_11)
    let (var_17: int64) = (int64 var_13)
    let (var_18: int64) = (int64 var_15)
    let (var_19: int32) = (var_11 * var_13)
    let (var_20: int32) = (var_19 * var_15)
    let (var_22: (uint8 [])) = var_5.ReadBytes(var_20)
    var_5.Dispose()
    var_4.Dispose()
    Tuple3(Tuple4(var_16, var_17, var_18), var_22)
and method_3((var_0: (uint8 [])), (var_1: (float32 [])), (var_2: int64)): unit =
    let (var_3: bool) = (var_2 < 10000L)
    if var_3 then
        let (var_4: bool) = (var_2 >= 0L)
        let (var_5: bool) = (var_4 = false)
        if var_5 then
            (failwith "Argument out of bounds.")
        else
            ()
        let (var_6: int64) = (var_2 * 784L)
        if var_5 then
            (failwith "Argument out of bounds.")
        else
            ()
        let (var_7: int64) = 0L
        method_4((var_0: (uint8 [])), (var_6: int64), (var_1: (float32 [])), (var_7: int64))
        let (var_8: int64) = (var_2 + 1L)
        method_3((var_0: (uint8 [])), (var_1: (float32 [])), (var_8: int64))
    else
        ()
and method_5((var_0: string)): Tuple5 =
    let (var_1: System.IO.FileMode) = System.IO.FileMode.Open
    let (var_2: System.IO.FileAccess) = System.IO.FileAccess.Read
    let (var_3: System.IO.FileShare) = System.IO.FileShare.Read
    let (var_4: System.IO.FileStream) = System.IO.File.Open(var_0, var_1, var_2, var_3)
    let (var_5: System.IO.BinaryReader) = System.IO.BinaryReader(var_4)
    let (var_6: int32) = var_5.ReadInt32()
    let (var_7: int32) = System.Net.IPAddress.NetworkToHostOrder(var_6)
    let (var_8: bool) = (var_7 = 2049)
    let (var_9: bool) = (var_8 = false)
    if var_9 then
        (failwith "Expected a 2049i32 magic number.")
    else
        ()
    let (var_10: int32) = var_5.ReadInt32()
    let (var_11: int32) = System.Net.IPAddress.NetworkToHostOrder(var_10)
    let (var_12: int64) = (int64 var_11)
    let (var_14: (uint8 [])) = var_5.ReadBytes(var_11)
    var_5.Dispose()
    var_4.Dispose()
    Tuple5(var_12, var_14)
and method_6((var_0: (uint8 [])), (var_1: (float32 [])), (var_2: int64)): unit =
    let (var_3: bool) = (var_2 < 10000L)
    if var_3 then
        let (var_4: bool) = (var_2 >= 0L)
        let (var_5: bool) = (var_4 = false)
        if var_5 then
            (failwith "Argument out of bounds.")
        else
            ()
        let (var_6: int64) = (var_2 * 10L)
        let (var_7: uint8) = var_0.[int32 var_2]
        let (var_8: int64) = 0L
        method_7((var_7: uint8), (var_1: (float32 [])), (var_6: int64), (var_8: int64))
        let (var_9: int64) = (var_2 + 1L)
        method_6((var_0: (uint8 [])), (var_1: (float32 [])), (var_9: int64))
    else
        ()
and method_8((var_0: (uint8 [])), (var_1: (float32 [])), (var_2: int64)): unit =
    let (var_3: bool) = (var_2 < 60000L)
    if var_3 then
        let (var_4: bool) = (var_2 >= 0L)
        let (var_5: bool) = (var_4 = false)
        if var_5 then
            (failwith "Argument out of bounds.")
        else
            ()
        let (var_6: int64) = (var_2 * 784L)
        if var_5 then
            (failwith "Argument out of bounds.")
        else
            ()
        let (var_7: int64) = 0L
        method_4((var_0: (uint8 [])), (var_6: int64), (var_1: (float32 [])), (var_7: int64))
        let (var_8: int64) = (var_2 + 1L)
        method_8((var_0: (uint8 [])), (var_1: (float32 [])), (var_8: int64))
    else
        ()
and method_9((var_0: (uint8 [])), (var_1: (float32 [])), (var_2: int64)): unit =
    let (var_3: bool) = (var_2 < 60000L)
    if var_3 then
        let (var_4: bool) = (var_2 >= 0L)
        let (var_5: bool) = (var_4 = false)
        if var_5 then
            (failwith "Argument out of bounds.")
        else
            ()
        let (var_6: int64) = (var_2 * 10L)
        let (var_7: uint8) = var_0.[int32 var_2]
        let (var_8: int64) = 0L
        method_7((var_7: uint8), (var_1: (float32 [])), (var_6: int64), (var_8: int64))
        let (var_9: int64) = (var_2 + 1L)
        method_9((var_0: (uint8 [])), (var_1: (float32 [])), (var_9: int64))
    else
        ()
and method_10((var_0: uint64), (var_1: System.Collections.Generic.Stack<Env2>), (var_2: uint64), (var_3: int64)): Env6 =
    let (var_4: int32) = var_1.get_Count()
    let (var_5: bool) = (var_4 > 0)
    if var_5 then
        let (var_6: Env2) = var_1.Peek()
        let (var_7: Env6) = var_6.mem_0
        let (var_8: int64) = var_6.mem_1
        let (var_9: (Union0 ref)) = var_7.mem_0
        let (var_10: Union0) = (!var_9)
        match var_10 with
        | Union0Case0(var_11) ->
            let (var_12: ManagedCuda.BasicTypes.CUdeviceptr) = var_11.mem_0
            method_11((var_12: ManagedCuda.BasicTypes.CUdeviceptr), (var_0: uint64), (var_2: uint64), (var_3: int64), (var_1: System.Collections.Generic.Stack<Env2>), (var_7: Env6), (var_8: int64))
        | Union0Case1 ->
            let (var_14: Env2) = var_1.Pop()
            let (var_15: Env6) = var_14.mem_0
            let (var_16: int64) = var_14.mem_1
            method_10((var_0: uint64), (var_1: System.Collections.Generic.Stack<Env2>), (var_2: uint64), (var_3: int64))
    else
        method_12((var_0: uint64), (var_2: uint64), (var_3: int64), (var_1: System.Collections.Generic.Stack<Env2>))
and method_13((var_0: (Union0 ref))): ManagedCuda.BasicTypes.CUdeviceptr =
    let (var_1: Union0) = (!var_0)
    match var_1 with
    | Union0Case0(var_2) ->
        var_2.mem_0
    | Union0Case1 ->
        (failwith "A Cuda memory cell that has been disposed has been tried to be accessed.")
and method_14((var_0: ManagedCuda.CudaContext), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: ManagedCuda.CudaStream), (var_3: uint64), (var_4: uint64), (var_5: System.Collections.Generic.Stack<Env2>), (var_6: ManagedCuda.CudaBlas.CudaBlasHandle), (var_7: (Union0 ref)), (var_8: (Union0 ref)), (var_9: (Union0 ref)), (var_10: (Union0 ref)), (var_11: float), (var_12: int64)): float =
    let (var_13: bool) = (var_12 < 60000L)
    if var_13 then
        let (var_14: int64) = (var_12 + 128L)
        let (var_15: bool) = (60000L > var_14)
        let (var_16: int64) =
            if var_15 then
                var_14
            else
                60000L
        let (var_17: bool) = (var_12 < var_16)
        let (var_18: bool) = (var_17 = false)
        if var_18 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_19: bool) = (var_12 >= 0L)
        let (var_20: bool) = (var_19 = false)
        if var_20 then
            (failwith "Lower boundary out of bounds.")
        else
            ()
        let (var_21: bool) = (var_16 > 0L)
        let (var_23: bool) =
            if var_21 then
                (var_16 <= 60000L)
            else
                false
        let (var_24: bool) = (var_23 = false)
        if var_24 then
            (failwith "Higher boundary out of bounds.")
        else
            ()
        let (var_25: int64) = (var_16 - var_12)
        let (var_26: int64) = (var_12 * 784L)
        if var_18 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        if var_20 then
            (failwith "Lower boundary out of bounds.")
        else
            ()
        let (var_28: bool) =
            if var_21 then
                (var_16 <= 60000L)
            else
                false
        let (var_29: bool) = (var_28 = false)
        if var_29 then
            (failwith "Higher boundary out of bounds.")
        else
            ()
        let (var_30: int64) = (var_12 * 10L)
        let (var_31: bool) = (var_25 > 0L)
        let (var_32: bool) = (var_31 = false)
        if var_32 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_33: int64) = (var_25 * 10L)
        let (var_34: int64) = (var_33 * 4L)
        let (var_35: Env6) = method_10((var_3: uint64), (var_5: System.Collections.Generic.Stack<Env2>), (var_4: uint64), (var_34: int64))
        let (var_36: (Union0 ref)) = var_35.mem_0
        let (var_37: int32) = (int32 var_25)
        method_15((var_6: ManagedCuda.CudaBlas.CudaBlasHandle), (var_37: int32), (var_9: (Union0 ref)), (var_26: int64), (var_25: int64), (var_8: (Union0 ref)), (var_36: (Union0 ref)))
        let (var_38: bool) = (0L < var_25)
        let (var_39: bool) = (var_38 = false)
        if var_39 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_40: Env6) = method_10((var_3: uint64), (var_5: System.Collections.Generic.Stack<Env2>), (var_4: uint64), (var_34: int64))
        let (var_41: (Union0 ref)) = var_40.mem_0
        let (var_42: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_41: (Union0 ref)))
        if var_39 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_43: ManagedCuda.BasicTypes.CUstream) = var_2.get_Stream()
        let (var_44: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_34)
        var_0.ClearMemoryAsync(var_42, 0uy, var_44, var_43)
        if var_39 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_49: Env6) = method_10((var_3: uint64), (var_5: System.Collections.Generic.Stack<Env2>), (var_4: uint64), (var_34: int64))
        let (var_50: (Union0 ref)) = var_49.mem_0
        let (var_51: bool) = (var_33 > 0L)
        let (var_52: bool) = (var_51 = false)
        if var_52 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_53: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_36: (Union0 ref)))
        if var_52 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_54: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_50: (Union0 ref)))
        let (var_55: int64) = (var_33 - 1L)
        let (var_56: int64) = (var_55 / 128L)
        let (var_57: int64) = (var_56 + 1L)
        let (var_58: bool) = (64L > var_57)
        let (var_59: int64) =
            if var_58 then
                var_57
            else
                64L
        // Cuda join point
        // method_16((var_53: ManagedCuda.BasicTypes.CUdeviceptr), (var_33: int64), (var_54: ManagedCuda.BasicTypes.CUdeviceptr))
        let (var_61: (System.Object [])) = [|var_53; var_33; var_54|]: (System.Object [])
        let (var_62: ManagedCuda.CudaKernel) = ManagedCuda.CudaKernel("method_16", var_1, var_0)
        let (var_63: uint32) = (uint32 var_59)
        let (var_64: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(var_63, 1u, 1u)
        var_62.set_GridDimensions(var_64)
        let (var_65: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(128u, 1u, 1u)
        var_62.set_BlockDimensions(var_65)
        let (var_66: ManagedCuda.BasicTypes.CUstream) = var_2.get_Stream()
        var_62.RunAsync(var_66, var_61)
        if var_39 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_67: Env6) = method_10((var_3: uint64), (var_5: System.Collections.Generic.Stack<Env2>), (var_4: uint64), (var_34: int64))
        let (var_68: (Union0 ref)) = var_67.mem_0
        let (var_69: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_68: (Union0 ref)))
        if var_39 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_70: ManagedCuda.BasicTypes.CUstream) = var_2.get_Stream()
        let (var_71: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_34)
        var_0.ClearMemoryAsync(var_69, 0uy, var_71, var_70)
        if var_52 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_72: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_50: (Union0 ref)))
        let (var_73: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_10: (Union0 ref)))
        let (var_74: int64) =
            if var_58 then
                var_57
            else
                64L
        let (var_77: bool) = (var_74 > 0L)
        let (var_78: bool) = (var_77 = false)
        if var_78 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_79: int64) = (var_74 * 4L)
        let (var_80: Env6) = method_10((var_3: uint64), (var_5: System.Collections.Generic.Stack<Env2>), (var_4: uint64), (var_79: int64))
        let (var_81: (Union0 ref)) = var_80.mem_0
        let (var_82: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_81: (Union0 ref)))
        // Cuda join point
        // method_18((var_72: ManagedCuda.BasicTypes.CUdeviceptr), (var_73: ManagedCuda.BasicTypes.CUdeviceptr), (var_33: int64), (var_82: ManagedCuda.BasicTypes.CUdeviceptr), (var_74: int64))
        let (var_84: (System.Object [])) = [|var_72; var_73; var_33; var_82; var_74|]: (System.Object [])
        let (var_85: ManagedCuda.CudaKernel) = ManagedCuda.CudaKernel("method_18", var_1, var_0)
        let (var_86: uint32) = (uint32 var_74)
        let (var_87: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(var_86, 1u, 1u)
        var_85.set_GridDimensions(var_87)
        let (var_88: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(128u, 1u, 1u)
        var_85.set_BlockDimensions(var_88)
        let (var_89: ManagedCuda.BasicTypes.CUstream) = var_2.get_Stream()
        var_85.RunAsync(var_89, var_84)
        let (var_91: (Union7 ref)) = (ref Union7Case1)
        let (var_92: (float32 ref)) = (ref 0.000000f)
        let (var_94: (Union7 ref)) = (ref Union7Case1)
        let (var_95: (float32 ref)) = (ref 0.000000f)
        let (var_96: float32) = method_25((var_25: int64), (var_81: (Union0 ref)), (var_74: int64), (var_0: ManagedCuda.CudaContext), (var_91: (Union7 ref)), (var_94: (Union7 ref)))
        let (var_97: string) = System.String.Format("{0}",var_16)
        let (var_98: string) = System.String.Format("{0} = {1}","near_to",var_97)
        let (var_99: string) = System.String.Format("{0}",var_12)
        let (var_100: string) = System.String.Format("{0} = {1}","from",var_99)
        let (var_101: string) = String.concat "; " [|var_100; var_98|]
        let (var_102: string) = System.String.Format("{0}{1}{2}","{",var_101,"}")
        let (var_103: string) = System.String.Format("On minibatch {0}. Error = {1}",var_102,var_96)
        let (var_104: string) = System.String.Format("{0}",var_103)
        System.Console.WriteLine(var_104)
        System.Console.WriteLine("Running the backwards phase...")
        var_95 := 1.000000f
        let (var_105: float32) = method_25((var_25: int64), (var_81: (Union0 ref)), (var_74: int64), (var_0: ManagedCuda.CudaContext), (var_91: (Union7 ref)), (var_94: (Union7 ref)))
        let (var_106: float32) = (!var_95)
        let (var_107: float32) = method_24((var_81: (Union0 ref)), (var_74: int64), (var_0: ManagedCuda.CudaContext), (var_91: (Union7 ref)))
        let (var_108: float32) = (float32 var_25)
        let (var_109: float32) = (var_106 / var_108)
        let (var_110: float32) = (!var_92)
        let (var_111: float32) = (var_110 + var_109)
        var_92 := var_111
        let (var_112: float32) = method_24((var_81: (Union0 ref)), (var_74: int64), (var_0: ManagedCuda.CudaContext), (var_91: (Union7 ref)))
        let (var_113: float32) = (!var_92)
        if var_52 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_114: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_68: (Union0 ref)))
        let (var_115: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_50: (Union0 ref)))
        let (var_116: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_10: (Union0 ref)))
        if var_52 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_117: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_68: (Union0 ref)))
        let (var_118: int64) =
            if var_58 then
                var_57
            else
                64L
        // Cuda join point
        // method_26((var_113: float32), (var_112: float32), (var_114: ManagedCuda.BasicTypes.CUdeviceptr), (var_115: ManagedCuda.BasicTypes.CUdeviceptr), (var_116: ManagedCuda.BasicTypes.CUdeviceptr), (var_33: int64), (var_117: ManagedCuda.BasicTypes.CUdeviceptr))
        let (var_120: (System.Object [])) = [|var_113; var_112; var_114; var_115; var_116; var_33; var_117|]: (System.Object [])
        let (var_121: ManagedCuda.CudaKernel) = ManagedCuda.CudaKernel("method_26", var_1, var_0)
        let (var_122: uint32) = (uint32 var_118)
        let (var_123: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(var_122, 1u, 1u)
        var_121.set_GridDimensions(var_123)
        let (var_124: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(128u, 1u, 1u)
        var_121.set_BlockDimensions(var_124)
        let (var_125: ManagedCuda.BasicTypes.CUstream) = var_2.get_Stream()
        var_121.RunAsync(var_125, var_120)
        if var_52 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_126: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_41: (Union0 ref)))
        let (var_127: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_36: (Union0 ref)))
        let (var_128: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_68: (Union0 ref)))
        let (var_129: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_50: (Union0 ref)))
        if var_52 then
            (failwith "Tensor needs to be at least size 1.")
        else
            ()
        let (var_130: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_41: (Union0 ref)))
        let (var_131: int64) =
            if var_58 then
                var_57
            else
                64L
        // Cuda join point
        // method_28((var_126: ManagedCuda.BasicTypes.CUdeviceptr), (var_127: ManagedCuda.BasicTypes.CUdeviceptr), (var_128: ManagedCuda.BasicTypes.CUdeviceptr), (var_129: ManagedCuda.BasicTypes.CUdeviceptr), (var_33: int64), (var_130: ManagedCuda.BasicTypes.CUdeviceptr))
        let (var_133: (System.Object [])) = [|var_126; var_127; var_128; var_129; var_33; var_130|]: (System.Object [])
        let (var_134: ManagedCuda.CudaKernel) = ManagedCuda.CudaKernel("method_28", var_1, var_0)
        let (var_135: uint32) = (uint32 var_131)
        let (var_136: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(var_135, 1u, 1u)
        var_134.set_GridDimensions(var_136)
        let (var_137: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(128u, 1u, 1u)
        var_134.set_BlockDimensions(var_137)
        let (var_138: ManagedCuda.BasicTypes.CUstream) = var_2.get_Stream()
        var_134.RunAsync(var_138, var_133)
        method_30((var_6: ManagedCuda.CudaBlas.CudaBlasHandle), (var_37: int32), (var_9: (Union0 ref)), (var_26: int64), (var_25: int64), (var_41: (Union0 ref)), (var_7: (Union0 ref)))
        let (var_139: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_7: (Union0 ref)))
        let (var_140: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_8: (Union0 ref)))
        let (var_141: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_8: (Union0 ref)))
        // Cuda join point
        // method_31((var_139: ManagedCuda.BasicTypes.CUdeviceptr), (var_140: ManagedCuda.BasicTypes.CUdeviceptr), (var_141: ManagedCuda.BasicTypes.CUdeviceptr))
        let (var_143: (System.Object [])) = [|var_139; var_140; var_141|]: (System.Object [])
        let (var_144: ManagedCuda.CudaKernel) = ManagedCuda.CudaKernel("method_31", var_1, var_0)
        let (var_145: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(62u, 1u, 1u)
        var_144.set_GridDimensions(var_145)
        let (var_146: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(128u, 1u, 1u)
        var_144.set_BlockDimensions(var_146)
        let (var_147: ManagedCuda.BasicTypes.CUstream) = var_2.get_Stream()
        var_144.RunAsync(var_147, var_143)
        let (var_148: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_7: (Union0 ref)))
        let (var_149: ManagedCuda.BasicTypes.CUstream) = var_2.get_Stream()
        let (var_150: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(31360L)
        var_0.ClearMemoryAsync(var_148, 0uy, var_150, var_149)
        let (var_151: float) = (float var_96)
        let (var_152: float) = (float var_25)
        let (var_153: float) = (var_151 * var_152)
        let (var_154: float) = (var_11 + var_153)
        var_81 := Union0Case1
        var_68 := Union0Case1
        var_50 := Union0Case1
        var_41 := Union0Case1
        var_36 := Union0Case1
        method_14((var_0: ManagedCuda.CudaContext), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: ManagedCuda.CudaStream), (var_3: uint64), (var_4: uint64), (var_5: System.Collections.Generic.Stack<Env2>), (var_6: ManagedCuda.CudaBlas.CudaBlasHandle), (var_7: (Union0 ref)), (var_8: (Union0 ref)), (var_9: (Union0 ref)), (var_10: (Union0 ref)), (var_154: float), (var_14: int64))
    else
        var_11
and method_4((var_0: (uint8 [])), (var_1: int64), (var_2: (float32 [])), (var_3: int64)): unit =
    let (var_4: bool) = (var_3 < 784L)
    if var_4 then
        let (var_5: bool) = (var_3 >= 0L)
        let (var_6: bool) = (var_5 = false)
        if var_6 then
            (failwith "Argument out of bounds.")
        else
            ()
        let (var_7: int64) = (var_1 + var_3)
        if var_6 then
            (failwith "Argument out of bounds.")
        else
            ()
        let (var_8: uint8) = var_0.[int32 var_7]
        let (var_9: float32) = (float32 var_8)
        let (var_10: float32) = (var_9 / 255.000000f)
        var_2.[int32 var_7] <- var_10
        let (var_11: int64) = (var_3 + 1L)
        method_4((var_0: (uint8 [])), (var_1: int64), (var_2: (float32 [])), (var_11: int64))
    else
        ()
and method_7((var_0: uint8), (var_1: (float32 [])), (var_2: int64), (var_3: int64)): unit =
    let (var_4: bool) = (var_3 < 10L)
    if var_4 then
        let (var_5: bool) = (var_3 >= 0L)
        let (var_6: bool) = (var_5 = false)
        if var_6 then
            (failwith "Argument out of bounds.")
        else
            ()
        let (var_7: int64) = (var_2 + var_3)
        let (var_8: uint8) = (uint8 var_3)
        let (var_9: bool) = (var_8 = var_0)
        let (var_10: float32) =
            if var_9 then
                1.000000f
            else
                0.000000f
        var_1.[int32 var_7] <- var_10
        let (var_11: int64) = (var_3 + 1L)
        method_7((var_0: uint8), (var_1: (float32 [])), (var_2: int64), (var_11: int64))
    else
        ()
and method_11((var_0: ManagedCuda.BasicTypes.CUdeviceptr), (var_1: uint64), (var_2: uint64), (var_3: int64), (var_4: System.Collections.Generic.Stack<Env2>), (var_5: Env6), (var_6: int64)): Env6 =
    let (var_7: ManagedCuda.BasicTypes.SizeT) = var_0.Pointer
    let (var_8: uint64) = uint64 var_7
    let (var_9: uint64) = uint64 var_6
    let (var_10: uint64) = (var_8 - var_1)
    let (var_11: uint64) = (var_10 + var_9)
    let (var_12: uint64) = uint64 var_3
    let (var_13: uint64) = (var_12 + var_11)
    let (var_14: bool) = (var_13 <= var_2)
    let (var_15: bool) = (var_14 = false)
    if var_15 then
        (failwith "Cache size has been exceeded in the allocator.")
    else
        ()
    let (var_16: uint64) = (var_8 + var_9)
    let (var_17: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_16)
    let (var_18: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_17)
    let (var_19: (Union0 ref)) = (ref (Union0Case0(Tuple1(var_18))))
    var_4.Push((Env2((Env6(var_19)), var_3)))
    (Env6(var_19))
and method_12((var_0: uint64), (var_1: uint64), (var_2: int64), (var_3: System.Collections.Generic.Stack<Env2>)): Env6 =
    let (var_4: uint64) = uint64 var_2
    let (var_5: bool) = (var_4 <= var_1)
    let (var_6: bool) = (var_5 = false)
    if var_6 then
        (failwith "Cache size has been exceeded in the allocator.")
    else
        ()
    let (var_7: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_0)
    let (var_8: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_7)
    let (var_9: (Union0 ref)) = (ref (Union0Case0(Tuple1(var_8))))
    var_3.Push((Env2((Env6(var_9)), var_2)))
    (Env6(var_9))
and method_15((var_0: ManagedCuda.CudaBlas.CudaBlasHandle), (var_1: int32), (var_2: (Union0 ref)), (var_3: int64), (var_4: int64), (var_5: (Union0 ref)), (var_6: (Union0 ref))): unit =
    let (var_7: ManagedCuda.CudaBlas.Operation) = ManagedCuda.CudaBlas.Operation.NonTranspose
    let (var_8: ManagedCuda.CudaBlas.Operation) = ManagedCuda.CudaBlas.Operation.NonTranspose
    let (var_9: (float32 ref)) = (ref 1.000000f)
    let (var_10: bool) = (0L < var_4)
    let (var_11: bool) = (var_10 = false)
    if var_11 then
        (failwith "Tensor needs to be at least size 1.")
    else
        ()
    let (var_12: int64) = (var_4 * 784L)
    let (var_13: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_2: (Union0 ref)))
    let (var_14: ManagedCuda.BasicTypes.SizeT) = var_13.Pointer
    let (var_15: uint64) = uint64 var_14
    let (var_16: uint64) = (uint64 var_3)
    let (var_17: uint64) = (var_15 + var_16)
    let (var_18: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_17)
    let (var_19: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_18)
    let (var_20: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_5: (Union0 ref)))
    let (var_21: (float32 ref)) = (ref 0.000000f)
    if var_11 then
        (failwith "Tensor needs to be at least size 1.")
    else
        ()
    let (var_22: int64) = (var_4 * 10L)
    let (var_23: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_6: (Union0 ref)))
    let (var_24: ManagedCuda.CudaBlas.CublasStatus) = ManagedCuda.CudaBlas.CudaBlasNativeMethods.cublasSgemm_v2(var_0, var_7, var_8, var_1, 10, 784, var_9, var_19, var_1, var_20, 784, var_21, var_23, var_1)
    if var_24 <> ManagedCuda.CudaBlas.CublasStatus.Success then raise <| new ManagedCuda.CudaBlas.CudaBlasException(var_24)
and method_25((var_0: int64), (var_1: (Union0 ref)), (var_2: int64), (var_3: ManagedCuda.CudaContext), (var_4: (Union7 ref)), (var_5: (Union7 ref))): float32 =
    let (var_6: Union7) = (!var_5)
    match var_6 with
    | Union7Case0(var_7) ->
        var_7.mem_0
    | Union7Case1 ->
        let (var_9: float32) = method_23((var_0: int64), (var_1: (Union0 ref)), (var_2: int64), (var_3: ManagedCuda.CudaContext), (var_4: (Union7 ref)))
        var_5 := (Union7Case0(Tuple8(var_9)))
        var_9
and method_24((var_0: (Union0 ref)), (var_1: int64), (var_2: ManagedCuda.CudaContext), (var_3: (Union7 ref))): float32 =
    let (var_4: Union7) = (!var_3)
    match var_4 with
    | Union7Case0(var_5) ->
        var_5.mem_0
    | Union7Case1 ->
        let (var_7: float32) = method_21((var_0: (Union0 ref)), (var_1: int64), (var_2: ManagedCuda.CudaContext))
        var_3 := (Union7Case0(Tuple8(var_7)))
        var_7
and method_30((var_0: ManagedCuda.CudaBlas.CudaBlasHandle), (var_1: int32), (var_2: (Union0 ref)), (var_3: int64), (var_4: int64), (var_5: (Union0 ref)), (var_6: (Union0 ref))): unit =
    let (var_7: ManagedCuda.CudaBlas.Operation) = ManagedCuda.CudaBlas.Operation.Transpose
    let (var_8: ManagedCuda.CudaBlas.Operation) = ManagedCuda.CudaBlas.Operation.NonTranspose
    let (var_9: (float32 ref)) = (ref 1.000000f)
    let (var_10: bool) = (0L < var_4)
    let (var_11: bool) = (var_10 = false)
    if var_11 then
        (failwith "Tensor needs to be at least size 1.")
    else
        ()
    let (var_12: int64) = (var_4 * 784L)
    let (var_13: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_2: (Union0 ref)))
    let (var_14: ManagedCuda.BasicTypes.SizeT) = var_13.Pointer
    let (var_15: uint64) = uint64 var_14
    let (var_16: uint64) = (uint64 var_3)
    let (var_17: uint64) = (var_15 + var_16)
    let (var_18: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_17)
    let (var_19: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_18)
    if var_11 then
        (failwith "Tensor needs to be at least size 1.")
    else
        ()
    let (var_20: int64) = (var_4 * 10L)
    let (var_21: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_5: (Union0 ref)))
    let (var_22: (float32 ref)) = (ref 1.000000f)
    let (var_23: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_6: (Union0 ref)))
    let (var_24: ManagedCuda.CudaBlas.CublasStatus) = ManagedCuda.CudaBlas.CudaBlasNativeMethods.cublasSgemm_v2(var_0, var_7, var_8, 784, 10, var_1, var_9, var_19, var_1, var_21, var_1, var_22, var_23, 784)
    if var_24 <> ManagedCuda.CudaBlas.CublasStatus.Success then raise <| new ManagedCuda.CudaBlas.CudaBlasException(var_24)
and method_23((var_0: int64), (var_1: (Union0 ref)), (var_2: int64), (var_3: ManagedCuda.CudaContext), (var_4: (Union7 ref))): float32 =
    let (var_5: float32) = method_24((var_1: (Union0 ref)), (var_2: int64), (var_3: ManagedCuda.CudaContext), (var_4: (Union7 ref)))
    let (var_6: float32) = (float32 var_0)
    (var_5 / var_6)
and method_21((var_0: (Union0 ref)), (var_1: int64), (var_2: ManagedCuda.CudaContext)): float32 =
    let (var_3: bool) = (0L < var_1)
    let (var_4: bool) = (var_3 = false)
    if var_4 then
        (failwith "Tensor needs to be at least size 1.")
    else
        ()
    let (var_5: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_0: (Union0 ref)))
    let (var_6: (float32 [])) = Array.zeroCreate<float32> (System.Convert.ToInt32(var_1))
    var_2.CopyToHost(var_6, var_5)
    var_2.Synchronize()
    let (var_7: float32) = 0.000000f
    let (var_8: int64) = 0L
    method_22((var_6: (float32 [])), (var_1: int64), (var_7: float32), (var_8: int64))
and method_22((var_0: (float32 [])), (var_1: int64), (var_2: float32), (var_3: int64)): float32 =
    let (var_4: bool) = (var_3 < var_1)
    if var_4 then
        let (var_5: bool) = (var_3 >= 0L)
        let (var_6: bool) = (var_5 = false)
        if var_6 then
            (failwith "Argument out of bounds.")
        else
            ()
        let (var_7: float32) = var_0.[int32 var_3]
        let (var_8: float32) = (var_2 + var_7)
        let (var_9: int64) = (var_3 + 1L)
        method_22((var_0: (float32 [])), (var_1: int64), (var_8: float32), (var_9: int64))
    else
        var_2
let (var_0: string) = cuda_kernels
let (var_1: ManagedCuda.CudaContext) = ManagedCuda.CudaContext(false)
var_1.Synchronize()
let (var_2: string) = System.Environment.get_CurrentDirectory()
let (var_3: string) = System.IO.Path.Combine(var_2, "nvcc_router.bat")
let (var_4: System.Diagnostics.ProcessStartInfo) = System.Diagnostics.ProcessStartInfo()
var_4.set_RedirectStandardOutput(true)
var_4.set_RedirectStandardError(true)
var_4.set_UseShellExecute(false)
var_4.set_FileName(var_3)
let (var_5: System.Diagnostics.Process) = System.Diagnostics.Process()
var_5.set_StartInfo(var_4)
let (var_7: (System.Diagnostics.DataReceivedEventArgs -> unit)) = method_0
var_5.OutputDataReceived.Add(var_7)
var_5.ErrorDataReceived.Add(var_7)
let (var_8: string) = System.IO.Path.Combine("C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Community", "VC\\Auxiliary\\Build\\vcvars64.bat")
let (var_9: string) = System.IO.Path.Combine("C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Community", "VC\\Tools\\MSVC\\14.11.25503\\bin\\Hostx64\\x64")
let (var_10: string) = System.IO.Path.Combine("C:\\Program Files\\NVIDIA GPU Computing Toolkit\\CUDA\\v9.0", "include")
let (var_11: string) = System.IO.Path.Combine("C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Community", "VC\\Tools\\MSVC\\14.11.25503\\include")
let (var_12: string) = System.IO.Path.Combine("C:\\Program Files\\NVIDIA GPU Computing Toolkit\\CUDA\\v9.0", "bin\\nvcc.exe")
let (var_13: string) = System.IO.Path.Combine(var_2, "cuda_kernels.ptx")
let (var_14: string) = System.IO.Path.Combine(var_2, "cuda_kernels.cu")
let (var_15: bool) = System.IO.File.Exists(var_14)
if var_15 then
    System.IO.File.Delete(var_14)
else
    ()
System.IO.File.WriteAllText(var_14, var_0)
let (var_16: bool) = System.IO.File.Exists(var_3)
if var_16 then
    System.IO.File.Delete(var_3)
else
    ()
let (var_17: System.IO.FileStream) = System.IO.File.OpenWrite(var_3)
let (var_18: System.IO.StreamWriter) = System.IO.StreamWriter(var_17)
var_18.WriteLine("SETLOCAL")
let (var_19: string) = String.concat "" [|"CALL "; "\""; var_8; "\""|]
var_18.WriteLine(var_19)
let (var_20: string) = String.concat "" [|"SET PATH=%PATH%;"; "\""; var_9; "\""|]
var_18.WriteLine(var_20)
let (var_21: string) = String.concat "" [|"\""; var_12; "\" -gencode=arch=compute_30,code=\\\"sm_30,compute_30\\\" --use-local-env --cl-version 2017 -I\""; var_10; "\" -I\"C:\\cub-1.7.4\" -I\""; var_11; "\" --keep-dir \""; var_2; "\" -maxrregcount=0  --machine 64 -ptx -cudart static  -o \""; var_13; "\" \""; var_14; "\""|]
var_18.WriteLine(var_21)
var_18.Dispose()
var_17.Dispose()
let (var_22: System.Diagnostics.Stopwatch) = System.Diagnostics.Stopwatch.StartNew()
let (var_23: bool) = var_5.Start()
let (var_24: bool) = (var_23 = false)
if var_24 then
    (failwith "NVCC failed to run.")
else
    ()
var_5.BeginOutputReadLine()
var_5.BeginErrorReadLine()
var_5.WaitForExit()
let (var_25: int32) = var_5.get_ExitCode()
let (var_26: bool) = (var_25 = 0)
let (var_27: bool) = (var_26 = false)
if var_27 then
    let (var_28: string) = System.String.Format("{0}",var_25)
    let (var_29: string) = String.concat ", " [|"NVCC failed compilation."; var_28|]
    let (var_30: string) = System.String.Format("[{0}]",var_29)
    (failwith var_30)
else
    ()
let (var_31: System.TimeSpan) = var_22.get_Elapsed()
printfn "The time it took to compile the Cuda kernels is: %A" var_31
let (var_32: ManagedCuda.BasicTypes.CUmodule) = var_1.LoadModulePTX(var_13)
var_5.Dispose()
let (var_33: string) = String.concat "" [|"Compiled the kernels into the following directory: "; var_2|]
let (var_34: string) = System.String.Format("{0}",var_33)
System.Console.WriteLine(var_34)
let (var_35: ManagedCuda.CudaDeviceProperties) = var_1.GetDeviceInfo()
let (var_36: ManagedCuda.BasicTypes.SizeT) = var_35.get_TotalGlobalMemory()
let (var_37: int64) = int64 var_36
let (var_38: float) = float var_37
let (var_39: float) = (0.700000 * var_38)
let (var_40: int64) = int64 var_39
let (var_41: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_40)
let (var_42: ManagedCuda.BasicTypes.CUdeviceptr) = var_1.AllocateMemory(var_41)
let (var_43: (Union0 ref)) = (ref (Union0Case0(Tuple1(var_42))))
let (var_44: System.Collections.Generic.Stack<Env2>) = System.Collections.Generic.Stack<Env2>()
let (var_45: ManagedCuda.BasicTypes.CUdeviceptr) = method_1((var_43: (Union0 ref)))
let (var_46: ManagedCuda.BasicTypes.SizeT) = var_45.Pointer
let (var_47: uint64) = uint64 var_46
let (var_48: uint64) = uint64 var_40
let (var_49: ManagedCuda.CudaStream) = ManagedCuda.CudaStream()
let (var_50: ManagedCuda.CudaRand.GeneratorType) = ManagedCuda.CudaRand.GeneratorType.PseudoDefault
let (var_51: ManagedCuda.CudaRand.CudaRandDevice) = ManagedCuda.CudaRand.CudaRandDevice(var_50)
let (var_52: ManagedCuda.BasicTypes.CUstream) = var_49.get_Stream()
var_51.SetStream(var_52)
let (var_53: ManagedCuda.CudaBlas.PointerMode) = ManagedCuda.CudaBlas.PointerMode.Host
let (var_54: ManagedCuda.CudaBlas.AtomicsMode) = ManagedCuda.CudaBlas.AtomicsMode.Allowed
let (var_55: ManagedCuda.CudaBlas.CudaBlas) = ManagedCuda.CudaBlas.CudaBlas(var_53, var_54)
let (var_56: ManagedCuda.CudaBlas.CudaBlasHandle) = var_55.get_CublasHandle()
let (var_57: ManagedCuda.BasicTypes.CUstream) = var_49.get_Stream()
var_55.set_Stream(var_57)
let (var_58: string) = System.IO.Path.Combine("C:\\ML Datasets\\Mnist", "t10k-images.idx3-ubyte")
let (var_59: Tuple3) = method_2((var_58: string))
let (var_60: Tuple4) = var_59.mem_0
let (var_61: (uint8 [])) = var_59.mem_1
let (var_62: int64) = var_60.mem_0
let (var_63: int64) = var_60.mem_1
let (var_64: int64) = var_60.mem_2
let (var_65: bool) = (10000L = var_62)
let (var_69: bool) =
    if var_65 then
        let (var_66: bool) = (28L = var_63)
        if var_66 then
            (28L = var_64)
        else
            false
    else
        false
let (var_70: bool) = (var_69 = false)
if var_70 then
    (failwith "Mnist dimensions do not match the expected values.")
else
    ()
let (var_71: int64) = var_61.LongLength
let (var_72: bool) = (var_71 > 0L)
let (var_73: bool) = (var_72 = false)
if var_73 then
    (failwith "Tensor needs to be at least size 1.")
else
    ()
let (var_74: bool) = (7840000L = var_71)
let (var_75: bool) = (var_74 = false)
if var_75 then
    (failwith "The product of given dimensions does not match the product of tensor dimensions.")
else
    ()
let (var_79: (float32 [])) = Array.zeroCreate<float32> (System.Convert.ToInt32(7840000L))
let (var_80: int64) = 0L
method_3((var_61: (uint8 [])), (var_79: (float32 [])), (var_80: int64))
let (var_81: string) = System.IO.Path.Combine("C:\\ML Datasets\\Mnist", "t10k-labels.idx1-ubyte")
let (var_82: Tuple5) = method_5((var_81: string))
let (var_83: int64) = var_82.mem_0
let (var_84: (uint8 [])) = var_82.mem_1
let (var_85: bool) = (10000L = var_83)
let (var_86: bool) = (var_85 = false)
if var_86 then
    (failwith "Mnist dimensions do not match the expected values.")
else
    ()
let (var_90: (float32 [])) = Array.zeroCreate<float32> (System.Convert.ToInt32(100000L))
let (var_91: int64) = 0L
method_6((var_84: (uint8 [])), (var_90: (float32 [])), (var_91: int64))
let (var_92: string) = System.IO.Path.Combine("C:\\ML Datasets\\Mnist", "train-images.idx3-ubyte")
let (var_93: Tuple3) = method_2((var_92: string))
let (var_94: Tuple4) = var_93.mem_0
let (var_95: (uint8 [])) = var_93.mem_1
let (var_96: int64) = var_94.mem_0
let (var_97: int64) = var_94.mem_1
let (var_98: int64) = var_94.mem_2
let (var_99: bool) = (60000L = var_96)
let (var_103: bool) =
    if var_99 then
        let (var_100: bool) = (28L = var_97)
        if var_100 then
            (28L = var_98)
        else
            false
    else
        false
let (var_104: bool) = (var_103 = false)
if var_104 then
    (failwith "Mnist dimensions do not match the expected values.")
else
    ()
let (var_105: int64) = var_95.LongLength
let (var_106: bool) = (var_105 > 0L)
let (var_107: bool) = (var_106 = false)
if var_107 then
    (failwith "Tensor needs to be at least size 1.")
else
    ()
let (var_108: bool) = (47040000L = var_105)
let (var_109: bool) = (var_108 = false)
if var_109 then
    (failwith "The product of given dimensions does not match the product of tensor dimensions.")
else
    ()
let (var_113: (float32 [])) = Array.zeroCreate<float32> (System.Convert.ToInt32(47040000L))
let (var_114: int64) = 0L
method_8((var_95: (uint8 [])), (var_113: (float32 [])), (var_114: int64))
let (var_115: string) = System.IO.Path.Combine("C:\\ML Datasets\\Mnist", "train-labels.idx1-ubyte")
let (var_116: Tuple5) = method_5((var_115: string))
let (var_117: int64) = var_116.mem_0
let (var_118: (uint8 [])) = var_116.mem_1
let (var_119: bool) = (60000L = var_117)
let (var_120: bool) = (var_119 = false)
if var_120 then
    (failwith "Mnist dimensions do not match the expected values.")
else
    ()
let (var_124: (float32 [])) = Array.zeroCreate<float32> (System.Convert.ToInt32(600000L))
let (var_125: int64) = 0L
method_9((var_118: (uint8 [])), (var_124: (float32 [])), (var_125: int64))
let (var_126: int64) = var_79.LongLength
let (var_127: int64) = (var_126 * 4L)
let (var_128: Env6) = method_10((var_47: uint64), (var_44: System.Collections.Generic.Stack<Env2>), (var_48: uint64), (var_127: int64))
let (var_129: (Union0 ref)) = var_128.mem_0
let (var_130: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_129: (Union0 ref)))
var_1.CopyToDevice(var_130, var_79)
let (var_131: int64) = var_90.LongLength
let (var_132: int64) = (var_131 * 4L)
let (var_133: Env6) = method_10((var_47: uint64), (var_44: System.Collections.Generic.Stack<Env2>), (var_48: uint64), (var_132: int64))
let (var_134: (Union0 ref)) = var_133.mem_0
let (var_135: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_134: (Union0 ref)))
var_1.CopyToDevice(var_135, var_90)
let (var_136: int64) = var_113.LongLength
let (var_137: int64) = (var_136 * 4L)
let (var_138: Env6) = method_10((var_47: uint64), (var_44: System.Collections.Generic.Stack<Env2>), (var_48: uint64), (var_137: int64))
let (var_139: (Union0 ref)) = var_138.mem_0
let (var_140: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_139: (Union0 ref)))
var_1.CopyToDevice(var_140, var_113)
let (var_141: int64) = var_124.LongLength
let (var_142: int64) = (var_141 * 4L)
let (var_143: Env6) = method_10((var_47: uint64), (var_44: System.Collections.Generic.Stack<Env2>), (var_48: uint64), (var_142: int64))
let (var_144: (Union0 ref)) = var_143.mem_0
let (var_145: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_144: (Union0 ref)))
var_1.CopyToDevice(var_145, var_124)
let (var_146: int64) = 31360L
let (var_147: Env6) = method_10((var_47: uint64), (var_44: System.Collections.Generic.Stack<Env2>), (var_48: uint64), (var_146: int64))
let (var_148: (Union0 ref)) = var_147.mem_0
let (var_149: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_148: (Union0 ref)))
let (var_150: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(7840L)
var_51.GenerateNormal32(var_149, var_150, 0.000000f, 0.050189f)
let (var_151: int64) = 31360L
let (var_152: Env6) = method_10((var_47: uint64), (var_44: System.Collections.Generic.Stack<Env2>), (var_48: uint64), (var_151: int64))
let (var_153: (Union0 ref)) = var_152.mem_0
let (var_154: ManagedCuda.BasicTypes.CUdeviceptr) = method_13((var_153: (Union0 ref)))
let (var_155: ManagedCuda.BasicTypes.CUstream) = var_49.get_Stream()
let (var_156: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(31360L)
var_1.ClearMemoryAsync(var_154, 0uy, var_156, var_155)
let (var_157: float) = 0.000000
let (var_158: int64) = 0L
let (var_159: float) = method_14((var_1: ManagedCuda.CudaContext), (var_32: ManagedCuda.BasicTypes.CUmodule), (var_49: ManagedCuda.CudaStream), (var_47: uint64), (var_48: uint64), (var_44: System.Collections.Generic.Stack<Env2>), (var_56: ManagedCuda.CudaBlas.CudaBlasHandle), (var_153: (Union0 ref)), (var_148: (Union0 ref)), (var_139: (Union0 ref)), (var_144: (Union0 ref)), (var_157: float), (var_158: int64))
System.Console.WriteLine("-----")
System.Console.WriteLine("Batch done.")
let (var_160: float) = (var_159 / 60000.000000)
let (var_161: string) = System.String.Format("Average of batch costs is {0}.",var_160)
let (var_162: string) = System.String.Format("{0}",var_161)
System.Console.WriteLine(var_162)
System.Console.WriteLine("-----")
var_153 := Union0Case1
var_148 := Union0Case1
var_55.Dispose()
var_51.Dispose()
var_49.Dispose()
let (var_163: ManagedCuda.BasicTypes.CUdeviceptr) = method_1((var_43: (Union0 ref)))
var_1.FreeMemory(var_163)
var_43 := Union0Case1
var_1.Dispose()

