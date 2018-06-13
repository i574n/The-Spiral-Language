module SpiralExample.Main
let cuda_kernels = """
#include "cub/cub.cuh"

extern "C" {
    __global__ void method_36(unsigned long long int * var_0, float * var_1);
    __global__ void method_42(int * var_0, int * var_1);
    __device__ char method_37(long long int * var_0);
    __device__ char method_43(long long int * var_0, int * var_1);
    
    __global__ void method_36(unsigned long long int * var_0, float * var_1) {
        long long int var_2 = threadIdx.x;
        long long int var_3 = blockIdx.x;
        long long int var_4 = (3 * var_3);
        long long int var_5 = (var_2 + var_4);
        long long int var_6[1];
        var_6[0] = var_5;
        while (method_37(var_6)) {
            long long int var_8 = var_6[0];
            long long int var_9 = (var_8 % 3);
            long long int var_10 = (var_8 / 3);
            char var_11 = (var_9 >= 0);
            char var_13;
            if (var_11) {
                var_13 = (var_9 < 3);
            } else {
                var_13 = 0;
            }
            char var_14 = (var_13 == 0);
            if (var_14) {
                // "Argument out of bounds."
            } else {
            }
            char var_16;
            if (var_11) {
                var_16 = (var_9 < 3);
            } else {
                var_16 = 0;
            }
            char var_17 = (var_16 == 0);
            if (var_17) {
                // "Argument out of bounds."
            } else {
            }
            long long int var_18 = (var_9 * 9);
            unsigned long long int var_19 = (unsigned long long) var_1 + var_18;
            var_0[var_9] = var_19;
            long long int var_20 = (var_8 + 3);
            var_6[0] = var_20;
        }
        long long int var_21 = var_6[0];
    }
    __global__ void method_42(int * var_0, int * var_1) {
        long long int var_2 = threadIdx.x;
        long long int var_3 = blockIdx.x;
        long long int var_4 = (3 * var_3);
        long long int var_5 = (var_2 + var_4);
        int var_6 = 0;
        long long int var_7[1];
        int var_8[1];
        var_7[0] = var_5;
        var_8[0] = var_6;
        while (method_43(var_7, var_8)) {
            long long int var_10 = var_7[0];
            int var_11 = var_8[0];
            char var_12 = (var_10 >= 0);
            char var_14;
            if (var_12) {
                var_14 = (var_10 < 3);
            } else {
                var_14 = 0;
            }
            char var_15 = (var_14 == 0);
            if (var_15) {
                // "Argument out of bounds."
            } else {
            }
            int var_16 = var_0[var_10];
            int var_17 = (var_11 + var_16);
            long long int var_18 = (var_10 + 3);
            var_7[0] = var_18;
            var_8[0] = var_17;
        }
        long long int var_19 = var_7[0];
        int var_20 = var_8[0];
        int var_21 = cub::BlockReduce<int,3,cub::BLOCK_REDUCE_WARP_REDUCTIONS,1,1>().Sum(var_20);
        long long int var_22 = threadIdx.x;
        char var_23 = (var_22 == 0);
        if (var_23) {
            long long int var_24 = blockIdx.x;
            char var_25 = (var_24 >= 0);
            char var_27;
            if (var_25) {
                var_27 = (var_24 < 1);
            } else {
                var_27 = 0;
            }
            char var_28 = (var_27 == 0);
            if (var_28) {
                // "Argument out of bounds."
            } else {
            }
            var_1[var_24] = var_21;
        } else {
        }
    }
    __device__ char method_37(long long int * var_0) {
        long long int var_1 = var_0[0];
        return (var_1 < 3);
    }
    __device__ char method_43(long long int * var_0, int * var_1) {
        long long int var_2 = var_0[0];
        int var_3 = var_1[0];
        return (var_2 < 3);
    }
}
"""

type EnvHeap0 =
    {
    mem_0: ManagedCuda.CudaContext
    }
and Env1 =
    struct
    val mem_0: uint64
    val mem_1: uint64
    new(arg_mem_0, arg_mem_1) = {mem_0 = arg_mem_0; mem_1 = arg_mem_1}
    end
and EnvStack2 =
    struct
    val mem_0: ResizeArray<Env1>
    new(arg_mem_0) = {mem_0 = arg_mem_0}
    end
and Env3 =
    struct
    val mem_0: Env20
    val mem_1: uint64
    new(arg_mem_0, arg_mem_1) = {mem_0 = arg_mem_0; mem_1 = arg_mem_1}
    end
and EnvStack4 =
    struct
    val mem_0: ResizeArray<Env3>
    new(arg_mem_0) = {mem_0 = arg_mem_0}
    end
and EnvHeap5 =
    {
    mem_0: EnvStack2
    mem_1: (uint64 ref)
    mem_2: uint64
    mem_3: EnvStack4
    }
and EnvHeap6 =
    {
    mem_0: ManagedCuda.CudaContext
    mem_1: EnvHeap5
    }
and EnvHeap7 =
    {
    mem_0: ManagedCuda.CudaContext
    mem_1: ManagedCuda.CudaRand.CudaRandDevice
    mem_2: EnvHeap5
    }
and EnvHeap8 =
    {
    mem_0: ManagedCuda.CudaContext
    mem_1: ManagedCuda.CudaBlas.CudaBlas
    mem_2: ManagedCuda.CudaRand.CudaRandDevice
    mem_3: EnvHeap5
    }
and Env9 =
    struct
    val mem_0: (int64 ref)
    val mem_1: Env20
    new(arg_mem_0, arg_mem_1) = {mem_0 = arg_mem_0; mem_1 = arg_mem_1}
    end
and EnvStack10 =
    struct
    val mem_0: ResizeArray<Env9>
    new(arg_mem_0) = {mem_0 = arg_mem_0}
    end
and EnvHeap11 =
    {
    mem_0: ManagedCuda.CudaContext
    mem_1: ManagedCuda.CudaBlas.CudaBlas
    mem_2: ManagedCuda.CudaRand.CudaRandDevice
    mem_3: EnvStack10
    mem_4: EnvHeap5
    }
and Env12 =
    struct
    val mem_0: (int64 ref)
    val mem_1: Env16
    new(arg_mem_0, arg_mem_1) = {mem_0 = arg_mem_0; mem_1 = arg_mem_1}
    end
and EnvStack13 =
    struct
    val mem_0: ResizeArray<Env12>
    new(arg_mem_0) = {mem_0 = arg_mem_0}
    end
and EnvHeap14 =
    {
    mem_0: ManagedCuda.CudaContext
    mem_1: ManagedCuda.CudaBlas.CudaBlas
    mem_2: ManagedCuda.CudaRand.CudaRandDevice
    mem_3: EnvStack10
    mem_4: EnvStack13
    mem_5: EnvHeap5
    }
and EnvHeap15 =
    {
    mem_0: ManagedCuda.CudaEvent
    mem_1: (bool ref)
    mem_2: ManagedCuda.CudaStream
    }
and Env16 =
    struct
    val mem_0: EnvHeap15
    new(arg_mem_0) = {mem_0 = arg_mem_0}
    end
and EnvHeap17 =
    {
    mem_0: ManagedCuda.CudaContext
    mem_1: ManagedCuda.CudaBlas.CudaBlas
    mem_2: ManagedCuda.CudaRand.CudaRandDevice
    mem_3: EnvStack10
    mem_4: EnvStack13
    mem_5: EnvHeap5
    mem_6: (int64 ref)
    mem_7: EnvHeap15
    }
and EnvStack18 =
    struct
    val mem_0: (int64 ref)
    val mem_1: (uint64 ref)
    new(arg_mem_0, arg_mem_1) = {mem_0 = arg_mem_0; mem_1 = arg_mem_1}
    end
and EnvStack19 =
    struct
    val mem_0: (int64 ref)
    val mem_1: (uint64 ref)
    new(arg_mem_0, arg_mem_1) = {mem_0 = arg_mem_0; mem_1 = arg_mem_1}
    end
and Env20 =
    struct
    val mem_0: (uint64 ref)
    new(arg_mem_0) = {mem_0 = arg_mem_0}
    end
and EnvStack21 =
    struct
    val mem_0: (int64 ref)
    val mem_1: (uint64 ref)
    new(arg_mem_0, arg_mem_1) = {mem_0 = arg_mem_0; mem_1 = arg_mem_1}
    end
let rec method_0 ((var_0: System.Diagnostics.DataReceivedEventArgs)): unit =
    let (var_1: string) = var_0.get_Data()
    System.Console.WriteLine(var_1)
and method_1((var_0: EnvHeap6), (var_1: ManagedCuda.BasicTypes.CUmodule)): unit =
    let (var_2: EnvHeap5) = var_0.mem_1
    let (var_3: (uint64 ref)) = var_2.mem_1
    let (var_4: uint64) = var_2.mem_2
    let (var_5: EnvStack2) = var_2.mem_0
    let (var_6: EnvStack4) = var_2.mem_3
    let (var_7: ResizeArray<Env3>) = var_6.mem_0
    let (var_9: (Env3 -> bool)) = method_2
    let (var_10: int32) = var_7.RemoveAll <| System.Predicate(var_9)
    let (var_12: (Env3 -> (Env3 -> int32))) = method_3
    let (var_13: System.Comparison<Env3>) = System.Comparison<Env3>(var_12)
    var_7.Sort(var_13)
    let (var_14: ResizeArray<Env1>) = var_5.mem_0
    var_14.Clear()
    let (var_15: int32) = var_7.get_Count()
    let (var_16: uint64) = method_5((var_3: (uint64 ref)))
    let (var_17: int32) = 0
    let (var_18: uint64) = method_6((var_5: EnvStack2), (var_6: EnvStack4), (var_15: int32), (var_16: uint64), (var_17: int32))
    let (var_19: uint64) = method_5((var_3: (uint64 ref)))
    let (var_20: uint64) = (var_19 + var_4)
    let (var_21: uint64) = (var_20 - var_18)
    let (var_22: uint64) = (var_18 + 256UL)
    let (var_23: uint64) = (var_22 - 1UL)
    let (var_24: uint64) = (var_23 &&& 18446744073709551360UL)
    let (var_25: uint64) = (var_24 - var_18)
    let (var_26: bool) = (var_21 > var_25)
    if var_26 then
        let (var_27: uint64) = (var_21 - var_25)
        var_14.Add((Env1(var_24, var_27)))
    else
        ()
and method_7((var_0: EnvHeap15), (var_1: EnvHeap14), (var_2: ManagedCuda.BasicTypes.CUmodule)): Env12 =
    let (var_3: (int64 ref)) = (ref 0L)
    let (var_4: EnvStack13) = var_1.mem_4
    method_8((var_3: (int64 ref)), (var_0: EnvHeap15), (var_4: EnvStack13))
    (Env12(var_3, (Env16(var_0))))
and method_9((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule)): EnvStack18 =
    let (var_2: Env9) = method_10((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule))
    let (var_3: (int64 ref)) = var_2.mem_0
    let (var_4: Env20) = var_2.mem_1
    let (var_5: (uint64 ref)) = var_4.mem_0
    method_17((var_3: (int64 ref)), (var_5: (uint64 ref)), (var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule))
    EnvStack18((var_3: (int64 ref)), (var_5: (uint64 ref)))
and method_19((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64), (var_11: int64), (var_12: int64), (var_13: int64)): unit =
    let (var_14: int64) = (var_9 - var_8)
    let (var_15: int64) = (var_11 - var_10)
    let (var_16: int64) = (var_13 - var_12)
    let (var_17: int64) = (var_14 * var_15)
    let (var_18: int64) = (var_17 * var_16)
    let (var_19: bool) = (var_18 > 0L)
    let (var_20: bool) = (var_19 = false)
    if var_20 then
        (failwith "Tensor needs to be at least size 1.")
    else
        ()
    let (var_21: int64) = (var_15 * var_6)
    let (var_22: bool) = (var_5 = var_21)
    let (var_23: bool) = (var_22 = false)
    if var_23 then
        (failwith "The tensor must be contiguous in order to be flattened.")
    else
        ()
    let (var_24: int64) = (var_14 * var_5)
    let (var_25: int64) = (var_16 * var_7)
    let (var_26: bool) = (var_6 = var_25)
    let (var_27: bool) = (var_26 = false)
    if var_27 then
        (failwith "The tensor must be contiguous in order to be flattened.")
    else
        ()
    let (var_28: int64) = (var_24 * var_6)
    let (var_29: (float32 [])) = method_20((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_14: int64), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64))
    let (var_30: int64) = 0L
    method_21((var_29: (float32 [])), (var_30: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64), (var_11: int64), (var_12: int64), (var_13: int64))
and method_31((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule)): EnvStack18 =
    let (var_4: Env9) = method_10((var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule))
    let (var_5: (int64 ref)) = var_4.mem_0
    let (var_6: Env20) = var_4.mem_1
    let (var_7: (uint64 ref)) = var_6.mem_0
    let (var_8: Env9) = method_32((var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule))
    let (var_9: (int64 ref)) = var_8.mem_0
    let (var_10: Env20) = var_8.mem_1
    let (var_11: (uint64 ref)) = var_10.mem_0
    let (var_12: Env9) = method_33((var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule))
    let (var_13: (int64 ref)) = var_12.mem_0
    let (var_14: Env20) = var_12.mem_1
    let (var_15: (uint64 ref)) = var_14.mem_0
    let (var_16: uint64) = method_5((var_15: (uint64 ref)))
    let (var_17: uint64) = method_5((var_1: (uint64 ref)))
    method_34((var_16: uint64), (var_17: uint64), (var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule))
    let (var_18: Env9) = method_33((var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule))
    let (var_19: (int64 ref)) = var_18.mem_0
    let (var_20: Env20) = var_18.mem_1
    let (var_21: (uint64 ref)) = var_20.mem_0
    let (var_22: uint64) = method_5((var_21: (uint64 ref)))
    let (var_23: uint64) = method_5((var_7: (uint64 ref)))
    method_34((var_22: uint64), (var_23: uint64), (var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule))
    method_38((var_13: (int64 ref)), (var_15: (uint64 ref)), (var_19: (int64 ref)), (var_21: (uint64 ref)), (var_9: (int64 ref)), (var_11: (uint64 ref)), (var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule))
    let (var_24: EnvStack21) = method_39((var_9: (int64 ref)), (var_11: (uint64 ref)), (var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule))
    let (var_25: (int64 ref)) = var_24.mem_0
    let (var_26: (uint64 ref)) = var_24.mem_1
    let (var_27: int64) = 1L
    let (var_28: int64) = 0L
    let (var_29: (int32 [])) = method_44((var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule), (var_27: int64), (var_25: (int64 ref)), (var_26: (uint64 ref)), (var_28: int64))
    let (var_30: int32) = var_29.[int32 0L]
    let (var_31: bool) = (var_30 = 0)
    let (var_32: bool) = (var_31 = false)
    if var_32 then
        (failwith "The matrix inversion failed.")
    else
        ()
    EnvStack18((var_5: (int64 ref)), (var_7: (uint64 ref)))
and method_45((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_4: EnvHeap17), (var_5: ManagedCuda.BasicTypes.CUmodule)): EnvStack19 =
    let (var_6: Env9) = method_46((var_4: EnvHeap17), (var_5: ManagedCuda.BasicTypes.CUmodule))
    let (var_7: (int64 ref)) = var_6.mem_0
    let (var_8: Env20) = var_6.mem_1
    let (var_9: (uint64 ref)) = var_8.mem_0
    method_47((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_7: (int64 ref)), (var_9: (uint64 ref)), (var_4: EnvHeap17), (var_5: ManagedCuda.BasicTypes.CUmodule))
    EnvStack19((var_7: (int64 ref)), (var_9: (uint64 ref)))
and method_48((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64)): unit =
    let (var_11: int64) = (var_8 - var_7)
    let (var_12: int64) = (var_10 - var_9)
    let (var_13: int64) = (var_11 * var_12)
    let (var_14: bool) = (var_13 > 0L)
    let (var_15: bool) = (var_14 = false)
    if var_15 then
        (failwith "Tensor needs to be at least size 1.")
    else
        ()
    let (var_16: int64) = (var_12 * var_6)
    let (var_17: bool) = (var_5 = var_16)
    let (var_18: bool) = (var_17 = false)
    if var_18 then
        (failwith "The tensor must be contiguous in order to be flattened.")
    else
        ()
    let (var_19: int64) = (var_11 * var_5)
    let (var_20: (float32 [])) = method_49((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_11: int64), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_4: int64), (var_5: int64), (var_6: int64))
    let (var_21: int64) = 0L
    method_50((var_20: (float32 [])), (var_21: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64))
and method_53((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_4: EnvHeap17), (var_5: ManagedCuda.BasicTypes.CUmodule)): EnvStack19 =
    let (var_6: Env9) = method_46((var_4: EnvHeap17), (var_5: ManagedCuda.BasicTypes.CUmodule))
    let (var_7: (int64 ref)) = var_6.mem_0
    let (var_8: Env20) = var_6.mem_1
    let (var_9: (uint64 ref)) = var_8.mem_0
    method_54((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_7: (int64 ref)), (var_9: (uint64 ref)), (var_4: EnvHeap17), (var_5: ManagedCuda.BasicTypes.CUmodule))
    EnvStack19((var_7: (int64 ref)), (var_9: (uint64 ref)))
and method_55((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_4: EnvHeap17), (var_5: ManagedCuda.BasicTypes.CUmodule)): EnvStack19 =
    let (var_6: Env9) = method_46((var_4: EnvHeap17), (var_5: ManagedCuda.BasicTypes.CUmodule))
    let (var_7: (int64 ref)) = var_6.mem_0
    let (var_8: Env20) = var_6.mem_1
    let (var_9: (uint64 ref)) = var_8.mem_0
    method_56((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_7: (int64 ref)), (var_9: (uint64 ref)), (var_4: EnvHeap17), (var_5: ManagedCuda.BasicTypes.CUmodule))
    EnvStack19((var_7: (int64 ref)), (var_9: (uint64 ref)))
and method_57((var_0: EnvStack13)): unit =
    let (var_1: ResizeArray<Env12>) = var_0.mem_0
    let (var_3: (Env12 -> unit)) = method_58
    var_1.ForEach <| System.Action<_>(var_3)
    var_1.Clear()
and method_59((var_0: EnvStack10)): unit =
    let (var_1: ResizeArray<Env9>) = var_0.mem_0
    let (var_3: (Env9 -> unit)) = method_60
    var_1.ForEach <| System.Action<_>(var_3)
    var_1.Clear()
and method_5((var_0: (uint64 ref))): uint64 =
    let (var_1: uint64) = (!var_0)
    let (var_2: bool) = (var_1 <> 0UL)
    let (var_3: bool) = (var_2 = false)
    if var_3 then
        (failwith "A Cuda memory cell that has been disposed has been tried to be accessed.")
    else
        ()
    var_1
and method_2 ((var_0: Env3)): bool =
    let (var_1: Env20) = var_0.mem_0
    let (var_2: (uint64 ref)) = var_1.mem_0
    let (var_3: uint64) = var_0.mem_1
    let (var_4: uint64) = (!var_2)
    (var_4 = 0UL)
and method_3 ((var_0: Env3)): (Env3 -> int32) =
    let (var_1: Env20) = var_0.mem_0
    let (var_2: (uint64 ref)) = var_1.mem_0
    let (var_3: uint64) = var_0.mem_1
    method_4((var_2: (uint64 ref)))
and method_6((var_0: EnvStack2), (var_1: EnvStack4), (var_2: int32), (var_3: uint64), (var_4: int32)): uint64 =
    let (var_5: bool) = (var_4 < var_2)
    if var_5 then
        let (var_6: ResizeArray<Env3>) = var_1.mem_0
        let (var_7: Env3) = var_6.[var_4]
        let (var_8: Env20) = var_7.mem_0
        let (var_9: (uint64 ref)) = var_8.mem_0
        let (var_10: uint64) = var_7.mem_1
        let (var_11: uint64) = method_5((var_9: (uint64 ref)))
        let (var_12: bool) = (var_11 >= var_3)
        let (var_13: bool) = (var_12 = false)
        if var_13 then
            (failwith "The next pointer should be higher than the last.")
        else
            ()
        let (var_14: uint64) = method_5((var_9: (uint64 ref)))
        let (var_15: uint64) = (var_14 - var_3)
        let (var_16: uint64) = (var_3 + 256UL)
        let (var_17: uint64) = (var_16 - 1UL)
        let (var_18: uint64) = (var_17 &&& 18446744073709551360UL)
        let (var_19: uint64) = (var_18 - var_3)
        let (var_20: bool) = (var_15 > var_19)
        if var_20 then
            let (var_21: ResizeArray<Env1>) = var_0.mem_0
            let (var_22: uint64) = (var_15 - var_19)
            var_21.Add((Env1(var_18, var_22)))
        else
            ()
        let (var_23: uint64) = (var_14 + var_10)
        let (var_24: int32) = (var_4 + 1)
        method_6((var_0: EnvStack2), (var_1: EnvStack4), (var_2: int32), (var_23: uint64), (var_24: int32))
    else
        var_3
and method_8((var_0: (int64 ref)), (var_1: EnvHeap15), (var_2: EnvStack13)): unit =
    let (var_3: int64) = (!var_0)
    let (var_4: int64) = (var_3 + 1L)
    var_0 := var_4
    let (var_5: ResizeArray<Env12>) = var_2.mem_0
    var_5.Add((Env12(var_0, (Env16(var_1)))))
and method_10((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule)): Env9 =
    let (var_2: int64) = 108L
    method_11((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: int64))
and method_17((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule)): unit =
    let (var_4: uint64) = method_5((var_1: (uint64 ref)))
    let (var_5: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(28L)
    let (var_6: ManagedCuda.CudaRand.CudaRandDevice) = var_2.mem_2
    let (var_7: (int64 ref)) = var_2.mem_6
    let (var_8: EnvHeap15) = var_2.mem_7
    let (var_9: ManagedCuda.BasicTypes.CUstream) = method_18((var_8: EnvHeap15))
    var_6.SetStream(var_9)
    let (var_10: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_4)
    let (var_11: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_10)
    var_6.GenerateNormal32(var_11, var_5, 3.000000f, 1.000000f)
and method_20((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: int64), (var_3: (int64 ref)), (var_4: (uint64 ref)), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64)): (float32 []) =
    let (var_9: (int64 ref)) = var_0.mem_6
    let (var_10: EnvHeap15) = var_0.mem_7
    let (var_11: ManagedCuda.BasicTypes.CUstream) = method_18((var_10: EnvHeap15))
    let (var_12: int64) = (var_2 * var_6)
    let (var_13: uint64) = method_5((var_4: (uint64 ref)))
    let (var_14: int64) = (var_5 * 4L)
    let (var_15: uint64) = (uint64 var_14)
    let (var_16: uint64) = (var_13 + var_15)
    let (var_17: (float32 [])) = Array.zeroCreate<float32> (System.Convert.ToInt32(var_12))
    let (var_18: System.Runtime.InteropServices.GCHandle) = System.Runtime.InteropServices.GCHandle.Alloc(var_17,System.Runtime.InteropServices.GCHandleType.Pinned)
    let (var_19: int64) = var_18.AddrOfPinnedObject().ToInt64()
    let (var_20: uint64) = (uint64 var_19)
    let (var_21: int64) = (var_12 * 4L)
    let (var_22: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_20)
    let (var_23: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_22)
    let (var_24: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_16)
    let (var_25: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_24)
    let (var_26: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_21)
    let (var_27: ManagedCuda.BasicTypes.CUResult) = ManagedCuda.DriverAPINativeMethods.AsynchronousMemcpy_v2.cuMemcpyAsync(var_23, var_25, var_26, var_11)
    if var_27 <> ManagedCuda.BasicTypes.CUResult.Success then raise <| new ManagedCuda.CudaException(var_27)
    var_18.Free()
    var_17
and method_21((var_0: (float32 [])), (var_1: int64), (var_2: int64), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64)): unit =
    let (var_11: System.Text.StringBuilder) = System.Text.StringBuilder()
    let (var_12: string) = ""
    let (var_13: int64) = 0L
    let (var_14: int64) = 0L
    method_22((var_11: System.Text.StringBuilder), (var_14: int64))
    let (var_15: System.Text.StringBuilder) = var_11.AppendLine("[|")
    let (var_16: int64) = method_23((var_11: System.Text.StringBuilder), (var_12: string), (var_0: (float32 [])), (var_1: int64), (var_2: int64), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64), (var_13: int64))
    let (var_17: int64) = 0L
    method_22((var_11: System.Text.StringBuilder), (var_17: int64))
    let (var_18: System.Text.StringBuilder) = var_11.AppendLine("|]")
    let (var_19: string) = var_11.ToString()
    System.Console.WriteLine(var_19)
and method_32((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule)): Env9 =
    let (var_2: int64) = 12L
    method_11((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: int64))
and method_33((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule)): Env9 =
    let (var_2: int64) = 24L
    method_11((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: int64))
and method_34((var_0: uint64), (var_1: uint64), (var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule)): unit =
    method_35((var_0: uint64), (var_1: uint64), (var_3: ManagedCuda.BasicTypes.CUmodule), (var_2: EnvHeap17))
and method_38((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_4: (int64 ref)), (var_5: (uint64 ref)), (var_6: EnvHeap17), (var_7: ManagedCuda.BasicTypes.CUmodule)): unit =
    let (var_8: ManagedCuda.CudaBlas.CudaBlas) = var_6.mem_1
    let (var_9: (int64 ref)) = var_6.mem_6
    let (var_10: EnvHeap15) = var_6.mem_7
    let (var_11: ManagedCuda.CudaBlas.CudaBlasHandle) = var_8.get_CublasHandle()
    let (var_12: ManagedCuda.BasicTypes.CUstream) = method_18((var_10: EnvHeap15))
    var_8.set_Stream(var_12)
    let (var_13: uint64) = method_5((var_1: (uint64 ref)))
    let (var_14: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_13)
    let (var_15: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_14)
    let (var_16: uint64) = method_5((var_3: (uint64 ref)))
    let (var_17: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_16)
    let (var_18: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_17)
    let (var_19: uint64) = method_5((var_5: (uint64 ref)))
    let (var_20: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_19)
    let (var_21: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_20)
    let (var_22: ManagedCuda.CudaBlas.CublasStatus) = ManagedCuda.CudaBlas.CudaBlasNativeMethods.cublasSmatinvBatched(var_11, 3, var_15, 3, var_18, 3, var_21, 3)
    if var_22 <> ManagedCuda.CudaBlas.CublasStatus.Success then raise <| new ManagedCuda.CudaBlas.CudaBlasException(var_22)
and method_39((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule)): EnvStack21 =
    let (var_4: uint64) = method_5((var_1: (uint64 ref)))
    let (var_5: Env9) = method_40((var_2: EnvHeap17), (var_3: ManagedCuda.BasicTypes.CUmodule))
    let (var_6: (int64 ref)) = var_5.mem_0
    let (var_7: Env20) = var_5.mem_1
    let (var_8: (uint64 ref)) = var_7.mem_0
    let (var_9: uint64) = method_5((var_8: (uint64 ref)))
    method_41((var_4: uint64), (var_9: uint64), (var_3: ManagedCuda.BasicTypes.CUmodule), (var_2: EnvHeap17))
    EnvStack21((var_6: (int64 ref)), (var_8: (uint64 ref)))
and method_44((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: int64), (var_3: (int64 ref)), (var_4: (uint64 ref)), (var_5: int64)): (int32 []) =
    let (var_6: (int64 ref)) = var_0.mem_6
    let (var_7: EnvHeap15) = var_0.mem_7
    let (var_8: ManagedCuda.BasicTypes.CUstream) = method_18((var_7: EnvHeap15))
    let (var_9: uint64) = method_5((var_4: (uint64 ref)))
    let (var_10: int64) = (var_5 * 4L)
    let (var_11: uint64) = (uint64 var_10)
    let (var_12: uint64) = (var_9 + var_11)
    let (var_13: (int32 [])) = Array.zeroCreate<int32> (System.Convert.ToInt32(var_2))
    let (var_14: System.Runtime.InteropServices.GCHandle) = System.Runtime.InteropServices.GCHandle.Alloc(var_13,System.Runtime.InteropServices.GCHandleType.Pinned)
    let (var_15: int64) = var_14.AddrOfPinnedObject().ToInt64()
    let (var_16: uint64) = (uint64 var_15)
    let (var_17: int64) = (var_2 * 4L)
    let (var_18: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_16)
    let (var_19: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_18)
    let (var_20: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_12)
    let (var_21: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_20)
    let (var_22: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_17)
    let (var_23: ManagedCuda.BasicTypes.CUResult) = ManagedCuda.DriverAPINativeMethods.AsynchronousMemcpy_v2.cuMemcpyAsync(var_19, var_21, var_22, var_8)
    if var_23 <> ManagedCuda.BasicTypes.CUResult.Success then raise <| new ManagedCuda.CudaException(var_23)
    var_14.Free()
    var_13
and method_46((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule)): Env9 =
    let (var_2: int64) = 36L
    method_11((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: int64))
and method_47((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_4: (int64 ref)), (var_5: (uint64 ref)), (var_6: EnvHeap17), (var_7: ManagedCuda.BasicTypes.CUmodule)): unit =
    let (var_8: ManagedCuda.CudaBlas.CudaBlas) = var_6.mem_1
    let (var_9: (int64 ref)) = var_6.mem_6
    let (var_10: EnvHeap15) = var_6.mem_7
    let (var_11: ManagedCuda.CudaBlas.CudaBlasHandle) = var_8.get_CublasHandle()
    let (var_12: ManagedCuda.BasicTypes.CUstream) = method_18((var_10: EnvHeap15))
    var_8.set_Stream(var_12)
    let (var_13: ManagedCuda.CudaBlas.Operation) = ManagedCuda.CudaBlas.Operation.NonTranspose
    let (var_14: ManagedCuda.CudaBlas.Operation) = ManagedCuda.CudaBlas.Operation.NonTranspose
    let (var_15: (float32 ref)) = (ref 1.000000f)
    let (var_16: uint64) = method_5((var_3: (uint64 ref)))
    let (var_17: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_16)
    let (var_18: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_17)
    let (var_19: uint64) = method_5((var_1: (uint64 ref)))
    let (var_20: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_19)
    let (var_21: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_20)
    let (var_22: (float32 ref)) = (ref 0.000000f)
    let (var_23: uint64) = method_5((var_5: (uint64 ref)))
    let (var_24: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_23)
    let (var_25: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_24)
    let (var_26: ManagedCuda.CudaBlas.CublasStatus) = ManagedCuda.CudaBlas.CudaBlasNativeMethods.cublasSgemm_v2(var_11, var_13, var_14, 3, 3, 3, var_15, var_18, 3, var_21, 3, var_22, var_25, 3)
    if var_26 <> ManagedCuda.CudaBlas.CublasStatus.Success then raise <| new ManagedCuda.CudaBlas.CudaBlasException(var_26)
and method_49((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: int64), (var_3: (int64 ref)), (var_4: (uint64 ref)), (var_5: int64), (var_6: int64), (var_7: int64)): (float32 []) =
    let (var_8: (int64 ref)) = var_0.mem_6
    let (var_9: EnvHeap15) = var_0.mem_7
    let (var_10: ManagedCuda.BasicTypes.CUstream) = method_18((var_9: EnvHeap15))
    let (var_11: int64) = (var_2 * var_6)
    let (var_12: uint64) = method_5((var_4: (uint64 ref)))
    let (var_13: int64) = (var_5 * 4L)
    let (var_14: uint64) = (uint64 var_13)
    let (var_15: uint64) = (var_12 + var_14)
    let (var_16: (float32 [])) = Array.zeroCreate<float32> (System.Convert.ToInt32(var_11))
    let (var_17: System.Runtime.InteropServices.GCHandle) = System.Runtime.InteropServices.GCHandle.Alloc(var_16,System.Runtime.InteropServices.GCHandleType.Pinned)
    let (var_18: int64) = var_17.AddrOfPinnedObject().ToInt64()
    let (var_19: uint64) = (uint64 var_18)
    let (var_20: int64) = (var_11 * 4L)
    let (var_21: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_19)
    let (var_22: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_21)
    let (var_23: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_15)
    let (var_24: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_23)
    let (var_25: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_20)
    let (var_26: ManagedCuda.BasicTypes.CUResult) = ManagedCuda.DriverAPINativeMethods.AsynchronousMemcpy_v2.cuMemcpyAsync(var_22, var_24, var_25, var_10)
    if var_26 <> ManagedCuda.BasicTypes.CUResult.Success then raise <| new ManagedCuda.CudaException(var_26)
    var_17.Free()
    var_16
and method_50((var_0: (float32 [])), (var_1: int64), (var_2: int64), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64)): unit =
    let (var_8: System.Text.StringBuilder) = System.Text.StringBuilder()
    let (var_9: string) = ""
    let (var_10: int64) = 0L
    let (var_11: int64) = 0L
    method_22((var_8: System.Text.StringBuilder), (var_11: int64))
    let (var_12: System.Text.StringBuilder) = var_8.AppendLine("[|")
    let (var_13: int64) = method_51((var_8: System.Text.StringBuilder), (var_9: string), (var_0: (float32 [])), (var_1: int64), (var_2: int64), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_10: int64))
    let (var_14: int64) = 0L
    method_22((var_8: System.Text.StringBuilder), (var_14: int64))
    let (var_15: System.Text.StringBuilder) = var_8.AppendLine("|]")
    let (var_16: string) = var_8.ToString()
    System.Console.WriteLine(var_16)
and method_54((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_4: (int64 ref)), (var_5: (uint64 ref)), (var_6: EnvHeap17), (var_7: ManagedCuda.BasicTypes.CUmodule)): unit =
    let (var_8: ManagedCuda.CudaBlas.CudaBlas) = var_6.mem_1
    let (var_9: (int64 ref)) = var_6.mem_6
    let (var_10: EnvHeap15) = var_6.mem_7
    let (var_11: ManagedCuda.CudaBlas.CudaBlasHandle) = var_8.get_CublasHandle()
    let (var_12: ManagedCuda.BasicTypes.CUstream) = method_18((var_10: EnvHeap15))
    var_8.set_Stream(var_12)
    let (var_13: ManagedCuda.CudaBlas.Operation) = ManagedCuda.CudaBlas.Operation.NonTranspose
    let (var_14: ManagedCuda.CudaBlas.Operation) = ManagedCuda.CudaBlas.Operation.NonTranspose
    let (var_15: (float32 ref)) = (ref 1.000000f)
    let (var_16: uint64) = method_5((var_3: (uint64 ref)))
    let (var_17: uint64) = (var_16 + 36UL)
    let (var_18: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_17)
    let (var_19: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_18)
    let (var_20: uint64) = method_5((var_1: (uint64 ref)))
    let (var_21: uint64) = (var_20 + 36UL)
    let (var_22: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_21)
    let (var_23: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_22)
    let (var_24: (float32 ref)) = (ref 0.000000f)
    let (var_25: uint64) = method_5((var_5: (uint64 ref)))
    let (var_26: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_25)
    let (var_27: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_26)
    let (var_28: ManagedCuda.CudaBlas.CublasStatus) = ManagedCuda.CudaBlas.CudaBlasNativeMethods.cublasSgemm_v2(var_11, var_13, var_14, 3, 3, 3, var_15, var_19, 3, var_23, 3, var_24, var_27, 3)
    if var_28 <> ManagedCuda.CudaBlas.CublasStatus.Success then raise <| new ManagedCuda.CudaBlas.CudaBlasException(var_28)
and method_56((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: (int64 ref)), (var_3: (uint64 ref)), (var_4: (int64 ref)), (var_5: (uint64 ref)), (var_6: EnvHeap17), (var_7: ManagedCuda.BasicTypes.CUmodule)): unit =
    let (var_8: ManagedCuda.CudaBlas.CudaBlas) = var_6.mem_1
    let (var_9: (int64 ref)) = var_6.mem_6
    let (var_10: EnvHeap15) = var_6.mem_7
    let (var_11: ManagedCuda.CudaBlas.CudaBlasHandle) = var_8.get_CublasHandle()
    let (var_12: ManagedCuda.BasicTypes.CUstream) = method_18((var_10: EnvHeap15))
    var_8.set_Stream(var_12)
    let (var_13: ManagedCuda.CudaBlas.Operation) = ManagedCuda.CudaBlas.Operation.NonTranspose
    let (var_14: ManagedCuda.CudaBlas.Operation) = ManagedCuda.CudaBlas.Operation.NonTranspose
    let (var_15: (float32 ref)) = (ref 1.000000f)
    let (var_16: uint64) = method_5((var_3: (uint64 ref)))
    let (var_17: uint64) = (var_16 + 72UL)
    let (var_18: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_17)
    let (var_19: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_18)
    let (var_20: uint64) = method_5((var_1: (uint64 ref)))
    let (var_21: uint64) = (var_20 + 72UL)
    let (var_22: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_21)
    let (var_23: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_22)
    let (var_24: (float32 ref)) = (ref 0.000000f)
    let (var_25: uint64) = method_5((var_5: (uint64 ref)))
    let (var_26: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_25)
    let (var_27: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_26)
    let (var_28: ManagedCuda.CudaBlas.CublasStatus) = ManagedCuda.CudaBlas.CudaBlasNativeMethods.cublasSgemm_v2(var_11, var_13, var_14, 3, 3, 3, var_15, var_19, 3, var_23, 3, var_24, var_27, 3)
    if var_28 <> ManagedCuda.CudaBlas.CublasStatus.Success then raise <| new ManagedCuda.CudaBlas.CudaBlasException(var_28)
and method_58 ((var_0: Env12)): unit =
    let (var_1: (int64 ref)) = var_0.mem_0
    let (var_2: Env16) = var_0.mem_1
    let (var_3: EnvHeap15) = var_2.mem_0
    let (var_4: int64) = (!var_1)
    let (var_5: int64) = (var_4 - 1L)
    var_1 := var_5
    let (var_6: int64) = (!var_1)
    let (var_7: bool) = (var_6 = 0L)
    if var_7 then
        let (var_8: ManagedCuda.CudaStream) = var_3.mem_2
        var_8.Dispose()
        let (var_9: ManagedCuda.CudaEvent) = var_3.mem_0
        var_9.Dispose()
        let (var_10: (bool ref)) = var_3.mem_1
        var_10 := false
    else
        ()
and method_60 ((var_0: Env9)): unit =
    let (var_1: (int64 ref)) = var_0.mem_0
    let (var_2: Env20) = var_0.mem_1
    let (var_3: (uint64 ref)) = var_2.mem_0
    let (var_4: int64) = (!var_1)
    let (var_5: int64) = (var_4 - 1L)
    var_1 := var_5
    let (var_6: int64) = (!var_1)
    let (var_7: bool) = (var_6 = 0L)
    if var_7 then
        var_3 := 0UL
    else
        ()
and method_4 ((var_1: (uint64 ref))) ((var_0: Env3)): int32 =
    let (var_2: Env20) = var_0.mem_0
    let (var_3: (uint64 ref)) = var_2.mem_0
    let (var_4: uint64) = var_0.mem_1
    let (var_5: uint64) = method_5((var_1: (uint64 ref)))
    let (var_6: uint64) = method_5((var_3: (uint64 ref)))
    let (var_7: bool) = (var_5 < var_6)
    if var_7 then
        -1
    else
        let (var_8: bool) = (var_5 = var_6)
        if var_8 then
            0
        else
            1
and method_11((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: int64)): Env9 =
    let (var_3: uint64) = (uint64 var_2)
    let (var_4: uint64) = (var_3 + 256UL)
    let (var_5: uint64) = (var_4 - 1UL)
    let (var_6: uint64) = (var_5 &&& 18446744073709551360UL)
    let (var_7: Env20) = method_12((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_6: uint64))
    let (var_8: (uint64 ref)) = var_7.mem_0
    let (var_9: (int64 ref)) = (ref 0L)
    let (var_10: EnvStack10) = var_0.mem_3
    method_16((var_9: (int64 ref)), (var_8: (uint64 ref)), (var_10: EnvStack10))
    (Env9(var_9, (Env20(var_8))))
and method_18((var_0: EnvHeap15)): ManagedCuda.BasicTypes.CUstream =
    let (var_1: (bool ref)) = var_0.mem_1
    let (var_2: bool) = (!var_1)
    let (var_3: bool) = (var_2 = false)
    if var_3 then
        (failwith "The stream has been disposed.")
    else
        ()
    let (var_4: ManagedCuda.CudaStream) = var_0.mem_2
    var_4.Stream
and method_22((var_0: System.Text.StringBuilder), (var_1: int64)): unit =
    let (var_2: bool) = (var_1 < 0L)
    if var_2 then
        let (var_3: System.Text.StringBuilder) = var_0.Append(' ')
        let (var_4: int64) = (var_1 + 1L)
        method_22((var_0: System.Text.StringBuilder), (var_4: int64))
    else
        ()
and method_23((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64), (var_11: int64), (var_12: int64), (var_13: int64)): int64 =
    let (var_14: bool) = (var_7 < var_8)
    if var_14 then
        let (var_15: bool) = (var_13 < 1000L)
        if var_15 then
            let (var_16: bool) = (var_7 >= var_7)
            let (var_17: bool) = (var_16 = false)
            if var_17 then
                (failwith "Argument out of bounds.")
            else
                ()
            let (var_18: int64) = 0L
            method_24((var_0: System.Text.StringBuilder), (var_18: int64))
            let (var_19: System.Text.StringBuilder) = var_0.AppendLine("[|")
            let (var_20: int64) = method_25((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_5: int64), (var_6: int64), (var_9: int64), (var_10: int64), (var_11: int64), (var_12: int64), (var_13: int64))
            let (var_21: int64) = 0L
            method_24((var_0: System.Text.StringBuilder), (var_21: int64))
            let (var_22: System.Text.StringBuilder) = var_0.AppendLine("|]")
            let (var_23: int64) = (var_7 + 1L)
            method_30((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64), (var_11: int64), (var_12: int64), (var_20: int64), (var_23: int64))
        else
            let (var_25: int64) = 0L
            method_22((var_0: System.Text.StringBuilder), (var_25: int64))
            let (var_26: System.Text.StringBuilder) = var_0.AppendLine("...")
            var_13
    else
        var_13
and method_35((var_0: uint64), (var_1: uint64), (var_2: ManagedCuda.BasicTypes.CUmodule), (var_3: EnvHeap17)): unit =
    // Cuda join point
    // method_36((var_0: uint64), (var_1: uint64))
    let (var_4: ManagedCuda.CudaContext) = var_3.mem_0
    let (var_5: ManagedCuda.CudaKernel) = ManagedCuda.CudaKernel("method_36", var_2, var_4)
    let (var_6: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(1u, 1u, 1u)
    var_5.set_GridDimensions(var_6)
    let (var_7: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(3u, 1u, 1u)
    var_5.set_BlockDimensions(var_7)
    let (var_8: (int64 ref)) = var_3.mem_6
    let (var_9: EnvHeap15) = var_3.mem_7
    let (var_10: ManagedCuda.BasicTypes.CUstream) = method_18((var_9: EnvHeap15))
    let (var_12: (System.Object [])) = [|var_0; var_1|]: (System.Object [])
    var_5.RunAsync(var_10, var_12)
and method_40((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule)): Env9 =
    let (var_2: int64) = 4L
    method_11((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: int64))
and method_41((var_0: uint64), (var_1: uint64), (var_2: ManagedCuda.BasicTypes.CUmodule), (var_3: EnvHeap17)): unit =
    // Cuda join point
    // method_42((var_0: uint64), (var_1: uint64))
    let (var_4: ManagedCuda.CudaContext) = var_3.mem_0
    let (var_5: ManagedCuda.CudaKernel) = ManagedCuda.CudaKernel("method_42", var_2, var_4)
    let (var_6: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(1u, 1u, 1u)
    var_5.set_GridDimensions(var_6)
    let (var_7: ManagedCuda.VectorTypes.dim3) = ManagedCuda.VectorTypes.dim3(3u, 1u, 1u)
    var_5.set_BlockDimensions(var_7)
    let (var_8: (int64 ref)) = var_3.mem_6
    let (var_9: EnvHeap15) = var_3.mem_7
    let (var_10: ManagedCuda.BasicTypes.CUstream) = method_18((var_9: EnvHeap15))
    let (var_12: (System.Object [])) = [|var_0; var_1|]: (System.Object [])
    var_5.RunAsync(var_10, var_12)
and method_51((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64)): int64 =
    let (var_11: bool) = (var_6 < var_7)
    if var_11 then
        let (var_12: bool) = (var_10 < 1000L)
        if var_12 then
            let (var_13: bool) = (var_6 >= var_6)
            let (var_14: bool) = (var_13 = false)
            if var_14 then
                (failwith "Argument out of bounds.")
            else
                ()
            let (var_15: int64) = 0L
            method_24((var_0: System.Text.StringBuilder), (var_15: int64))
            let (var_16: System.Text.StringBuilder) = var_0.Append("[|")
            let (var_17: int64) = method_27((var_0: System.Text.StringBuilder), (var_2: (float32 [])), (var_3: int64), (var_5: int64), (var_8: int64), (var_9: int64), (var_1: string), (var_10: int64))
            let (var_18: System.Text.StringBuilder) = var_0.AppendLine("|]")
            let (var_19: int64) = (var_6 + 1L)
            method_52((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_17: int64), (var_19: int64))
        else
            let (var_21: int64) = 0L
            method_22((var_0: System.Text.StringBuilder), (var_21: int64))
            let (var_22: System.Text.StringBuilder) = var_0.AppendLine("...")
            var_10
    else
        var_10
and method_12((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule), (var_2: uint64)): Env20 =
    let (var_3: EnvHeap5) = var_0.mem_5
    let (var_4: (uint64 ref)) = var_3.mem_1
    let (var_5: uint64) = var_3.mem_2
    let (var_6: EnvStack4) = var_3.mem_3
    let (var_7: EnvStack2) = var_3.mem_0
    let (var_8: ResizeArray<Env1>) = var_7.mem_0
    let (var_9: int32) = var_8.get_Count()
    let (var_10: bool) = (var_9 > 0)
    let (var_11: bool) = (var_10 = false)
    if var_11 then
        (failwith "Out of memory in the designated section.")
    else
        ()
    let (var_12: Env1) = var_8.[0]
    let (var_13: uint64) = var_12.mem_0
    let (var_14: uint64) = var_12.mem_1
    let (var_15: bool) = (var_2 <= var_14)
    let (var_42: Env3) =
        if var_15 then
            let (var_16: uint64) = (var_13 + var_2)
            let (var_17: uint64) = (var_14 - var_2)
            var_8.[0] <- (Env1(var_16, var_17))
            let (var_18: (uint64 ref)) = (ref var_13)
            (Env3((Env20(var_18)), var_2))
        else
            let (var_20: (Env1 -> (Env1 -> int32))) = method_13
            let (var_21: System.Comparison<Env1>) = System.Comparison<Env1>(var_20)
            var_8.Sort(var_21)
            let (var_22: Env1) = var_8.[0]
            let (var_23: uint64) = var_22.mem_0
            let (var_24: uint64) = var_22.mem_1
            let (var_25: bool) = (var_2 <= var_24)
            if var_25 then
                let (var_26: uint64) = (var_23 + var_2)
                let (var_27: uint64) = (var_24 - var_2)
                var_8.[0] <- (Env1(var_26, var_27))
                let (var_28: (uint64 ref)) = (ref var_23)
                (Env3((Env20(var_28)), var_2))
            else
                method_15((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule))
                let (var_30: (Env1 -> (Env1 -> int32))) = method_13
                let (var_31: System.Comparison<Env1>) = System.Comparison<Env1>(var_30)
                var_8.Sort(var_31)
                let (var_32: Env1) = var_8.[0]
                let (var_33: uint64) = var_32.mem_0
                let (var_34: uint64) = var_32.mem_1
                let (var_35: bool) = (var_2 <= var_34)
                if var_35 then
                    let (var_36: uint64) = (var_33 + var_2)
                    let (var_37: uint64) = (var_34 - var_2)
                    var_8.[0] <- (Env1(var_36, var_37))
                    let (var_38: (uint64 ref)) = (ref var_33)
                    (Env3((Env20(var_38)), var_2))
                else
                    (failwith "Out of memory in the designated section.")
    let (var_43: Env20) = var_42.mem_0
    let (var_44: (uint64 ref)) = var_43.mem_0
    let (var_45: uint64) = var_42.mem_1
    let (var_46: ResizeArray<Env3>) = var_6.mem_0
    var_46.Add((Env3((Env20(var_44)), var_45)))
    (Env20(var_44))
and method_16((var_0: (int64 ref)), (var_1: (uint64 ref)), (var_2: EnvStack10)): unit =
    let (var_3: int64) = (!var_0)
    let (var_4: int64) = (var_3 + 1L)
    var_0 := var_4
    let (var_5: ResizeArray<Env9>) = var_2.mem_0
    var_5.Add((Env9(var_0, (Env20(var_1)))))
and method_24((var_0: System.Text.StringBuilder), (var_1: int64)): unit =
    let (var_2: bool) = (var_1 < 4L)
    if var_2 then
        let (var_3: System.Text.StringBuilder) = var_0.Append(' ')
        let (var_4: int64) = (var_1 + 1L)
        method_24((var_0: System.Text.StringBuilder), (var_4: int64))
    else
        ()
and method_25((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64)): int64 =
    let (var_11: bool) = (var_6 < var_7)
    if var_11 then
        let (var_12: bool) = (var_10 < 1000L)
        if var_12 then
            let (var_13: bool) = (var_6 >= var_6)
            let (var_14: bool) = (var_13 = false)
            if var_14 then
                (failwith "Argument out of bounds.")
            else
                ()
            let (var_15: int64) = 0L
            method_26((var_0: System.Text.StringBuilder), (var_15: int64))
            let (var_16: System.Text.StringBuilder) = var_0.Append("[|")
            let (var_17: int64) = method_27((var_0: System.Text.StringBuilder), (var_2: (float32 [])), (var_3: int64), (var_5: int64), (var_8: int64), (var_9: int64), (var_1: string), (var_10: int64))
            let (var_18: System.Text.StringBuilder) = var_0.AppendLine("|]")
            let (var_19: int64) = (var_6 + 1L)
            method_29((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_17: int64), (var_19: int64))
        else
            let (var_21: int64) = 0L
            method_24((var_0: System.Text.StringBuilder), (var_21: int64))
            let (var_22: System.Text.StringBuilder) = var_0.AppendLine("...")
            var_10
    else
        var_10
and method_30((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64), (var_11: int64), (var_12: int64), (var_13: int64), (var_14: int64)): int64 =
    let (var_15: bool) = (var_14 < var_8)
    if var_15 then
        let (var_16: bool) = (var_13 < 1000L)
        if var_16 then
            let (var_17: bool) = (var_14 >= var_7)
            let (var_18: bool) = (var_17 = false)
            if var_18 then
                (failwith "Argument out of bounds.")
            else
                ()
            let (var_19: int64) = (var_14 - var_7)
            let (var_20: int64) = (var_19 * var_4)
            let (var_21: int64) = (var_3 + var_20)
            let (var_22: int64) = 0L
            method_24((var_0: System.Text.StringBuilder), (var_22: int64))
            let (var_23: System.Text.StringBuilder) = var_0.AppendLine("[|")
            let (var_24: int64) = method_25((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_21: int64), (var_5: int64), (var_6: int64), (var_9: int64), (var_10: int64), (var_11: int64), (var_12: int64), (var_13: int64))
            let (var_25: int64) = 0L
            method_24((var_0: System.Text.StringBuilder), (var_25: int64))
            let (var_26: System.Text.StringBuilder) = var_0.AppendLine("|]")
            let (var_27: int64) = (var_14 + 1L)
            method_30((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64), (var_11: int64), (var_12: int64), (var_24: int64), (var_27: int64))
        else
            let (var_29: int64) = 0L
            method_22((var_0: System.Text.StringBuilder), (var_29: int64))
            let (var_30: System.Text.StringBuilder) = var_0.AppendLine("...")
            var_13
    else
        var_13
and method_27((var_0: System.Text.StringBuilder), (var_1: (float32 [])), (var_2: int64), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: string), (var_7: int64)): int64 =
    let (var_8: bool) = (var_4 < var_5)
    if var_8 then
        let (var_9: bool) = (var_7 < 1000L)
        if var_9 then
            let (var_10: System.Text.StringBuilder) = var_0.Append(var_6)
            let (var_11: bool) = (var_4 >= var_4)
            let (var_12: bool) = (var_11 = false)
            if var_12 then
                (failwith "Argument out of bounds.")
            else
                ()
            let (var_13: float32) = var_1.[int32 var_2]
            let (var_14: string) = System.String.Format("{0}",var_13)
            let (var_15: System.Text.StringBuilder) = var_0.Append(var_14)
            let (var_16: string) = "; "
            let (var_17: int64) = (var_7 + 1L)
            let (var_18: int64) = (var_4 + 1L)
            method_28((var_0: System.Text.StringBuilder), (var_1: (float32 [])), (var_2: int64), (var_3: int64), (var_4: int64), (var_5: int64), (var_16: string), (var_17: int64), (var_18: int64))
        else
            let (var_20: System.Text.StringBuilder) = var_0.Append("...")
            var_7
    else
        var_7
and method_52((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64), (var_11: int64)): int64 =
    let (var_12: bool) = (var_11 < var_7)
    if var_12 then
        let (var_13: bool) = (var_10 < 1000L)
        if var_13 then
            let (var_14: bool) = (var_11 >= var_6)
            let (var_15: bool) = (var_14 = false)
            if var_15 then
                (failwith "Argument out of bounds.")
            else
                ()
            let (var_16: int64) = (var_11 - var_6)
            let (var_17: int64) = (var_16 * var_4)
            let (var_18: int64) = (var_3 + var_17)
            let (var_19: int64) = 0L
            method_24((var_0: System.Text.StringBuilder), (var_19: int64))
            let (var_20: System.Text.StringBuilder) = var_0.Append("[|")
            let (var_21: int64) = method_27((var_0: System.Text.StringBuilder), (var_2: (float32 [])), (var_18: int64), (var_5: int64), (var_8: int64), (var_9: int64), (var_1: string), (var_10: int64))
            let (var_22: System.Text.StringBuilder) = var_0.AppendLine("|]")
            let (var_23: int64) = (var_11 + 1L)
            method_52((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_21: int64), (var_23: int64))
        else
            let (var_25: int64) = 0L
            method_22((var_0: System.Text.StringBuilder), (var_25: int64))
            let (var_26: System.Text.StringBuilder) = var_0.AppendLine("...")
            var_10
    else
        var_10
and method_13 ((var_0: Env1)): (Env1 -> int32) =
    let (var_1: uint64) = var_0.mem_0
    let (var_2: uint64) = var_0.mem_1
    method_14((var_2: uint64))
and method_15((var_0: EnvHeap17), (var_1: ManagedCuda.BasicTypes.CUmodule)): unit =
    let (var_2: EnvHeap5) = var_0.mem_5
    let (var_3: (uint64 ref)) = var_2.mem_1
    let (var_4: uint64) = var_2.mem_2
    let (var_5: EnvStack2) = var_2.mem_0
    let (var_6: EnvStack4) = var_2.mem_3
    let (var_7: ResizeArray<Env3>) = var_6.mem_0
    let (var_9: (Env3 -> bool)) = method_2
    let (var_10: int32) = var_7.RemoveAll <| System.Predicate(var_9)
    let (var_12: (Env3 -> (Env3 -> int32))) = method_3
    let (var_13: System.Comparison<Env3>) = System.Comparison<Env3>(var_12)
    var_7.Sort(var_13)
    let (var_14: ResizeArray<Env1>) = var_5.mem_0
    var_14.Clear()
    let (var_15: int32) = var_7.get_Count()
    let (var_16: uint64) = method_5((var_3: (uint64 ref)))
    let (var_17: int32) = 0
    let (var_18: uint64) = method_6((var_5: EnvStack2), (var_6: EnvStack4), (var_15: int32), (var_16: uint64), (var_17: int32))
    let (var_19: uint64) = method_5((var_3: (uint64 ref)))
    let (var_20: uint64) = (var_19 + var_4)
    let (var_21: uint64) = (var_20 - var_18)
    let (var_22: uint64) = (var_18 + 256UL)
    let (var_23: uint64) = (var_22 - 1UL)
    let (var_24: uint64) = (var_23 &&& 18446744073709551360UL)
    let (var_25: uint64) = (var_24 - var_18)
    let (var_26: bool) = (var_21 > var_25)
    if var_26 then
        let (var_27: uint64) = (var_21 - var_25)
        var_14.Add((Env1(var_24, var_27)))
    else
        ()
and method_26((var_0: System.Text.StringBuilder), (var_1: int64)): unit =
    let (var_2: bool) = (var_1 < 8L)
    if var_2 then
        let (var_3: System.Text.StringBuilder) = var_0.Append(' ')
        let (var_4: int64) = (var_1 + 1L)
        method_26((var_0: System.Text.StringBuilder), (var_4: int64))
    else
        ()
and method_29((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_10: int64), (var_11: int64)): int64 =
    let (var_12: bool) = (var_11 < var_7)
    if var_12 then
        let (var_13: bool) = (var_10 < 1000L)
        if var_13 then
            let (var_14: bool) = (var_11 >= var_6)
            let (var_15: bool) = (var_14 = false)
            if var_15 then
                (failwith "Argument out of bounds.")
            else
                ()
            let (var_16: int64) = (var_11 - var_6)
            let (var_17: int64) = (var_16 * var_4)
            let (var_18: int64) = (var_3 + var_17)
            let (var_19: int64) = 0L
            method_26((var_0: System.Text.StringBuilder), (var_19: int64))
            let (var_20: System.Text.StringBuilder) = var_0.Append("[|")
            let (var_21: int64) = method_27((var_0: System.Text.StringBuilder), (var_2: (float32 [])), (var_18: int64), (var_5: int64), (var_8: int64), (var_9: int64), (var_1: string), (var_10: int64))
            let (var_22: System.Text.StringBuilder) = var_0.AppendLine("|]")
            let (var_23: int64) = (var_11 + 1L)
            method_29((var_0: System.Text.StringBuilder), (var_1: string), (var_2: (float32 [])), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: int64), (var_7: int64), (var_8: int64), (var_9: int64), (var_21: int64), (var_23: int64))
        else
            let (var_25: int64) = 0L
            method_24((var_0: System.Text.StringBuilder), (var_25: int64))
            let (var_26: System.Text.StringBuilder) = var_0.AppendLine("...")
            var_10
    else
        var_10
and method_28((var_0: System.Text.StringBuilder), (var_1: (float32 [])), (var_2: int64), (var_3: int64), (var_4: int64), (var_5: int64), (var_6: string), (var_7: int64), (var_8: int64)): int64 =
    let (var_9: bool) = (var_8 < var_5)
    if var_9 then
        let (var_10: bool) = (var_7 < 1000L)
        if var_10 then
            let (var_11: System.Text.StringBuilder) = var_0.Append(var_6)
            let (var_12: bool) = (var_8 >= var_4)
            let (var_13: bool) = (var_12 = false)
            if var_13 then
                (failwith "Argument out of bounds.")
            else
                ()
            let (var_14: int64) = (var_8 - var_4)
            let (var_15: int64) = (var_14 * var_3)
            let (var_16: int64) = (var_2 + var_15)
            let (var_17: float32) = var_1.[int32 var_16]
            let (var_18: string) = System.String.Format("{0}",var_17)
            let (var_19: System.Text.StringBuilder) = var_0.Append(var_18)
            let (var_20: string) = "; "
            let (var_21: int64) = (var_7 + 1L)
            let (var_22: int64) = (var_8 + 1L)
            method_28((var_0: System.Text.StringBuilder), (var_1: (float32 [])), (var_2: int64), (var_3: int64), (var_4: int64), (var_5: int64), (var_20: string), (var_21: int64), (var_22: int64))
        else
            let (var_24: System.Text.StringBuilder) = var_0.Append("...")
            var_7
    else
        var_7
and method_14 ((var_1: uint64)) ((var_0: Env1)): int32 =
    let (var_2: uint64) = var_0.mem_0
    let (var_3: uint64) = var_0.mem_1
    let (var_4: bool) = (var_3 < var_1)
    if var_4 then
        -1
    else
        let (var_5: bool) = (var_3 = var_1)
        if var_5 then
            0
        else
            1
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
let (var_8: string) = System.IO.Path.Combine("C:/Program Files (x86)/Microsoft Visual Studio/2017/Community", "VC/Auxiliary/Build/vcvarsall.bat")
let (var_9: string) = System.IO.Path.Combine("C:/Program Files (x86)/Microsoft Visual Studio/2017/Community", "VC/Tools/MSVC/14.11.25503/bin/Hostx64/x64")
let (var_10: string) = System.IO.Path.Combine("C:/Program Files (x86)/Microsoft Visual Studio/2017/Community", "VC/Tools/MSVC/14.11.25503/include")
let (var_11: string) = System.IO.Path.Combine("C:/Program Files/NVIDIA GPU Computing Toolkit/CUDA/v9.0", "include")
let (var_12: string) = System.IO.Path.Combine("C:/Program Files/NVIDIA GPU Computing Toolkit/CUDA/v9.0", "bin/nvcc.exe")
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
let (var_19: string) = String.concat "" [|"CALL "; "\""; var_8; "\" x64 -vcvars_ver=14.11"|]
var_18.WriteLine(var_19)
let (var_20: string) = String.concat "" [|"SET PATH=%PATH%;"; "\""; var_9; "\""|]
var_18.WriteLine(var_20)
let (var_21: string) = String.concat "" [|"\""; var_12; "\" -gencode=arch=compute_52,code=\\\"sm_52,compute_52\\\" --use-local-env --cl-version 2017 -I\""; var_11; "\" -I\"C:/cub-1.7.4\" -I\""; var_10; "\" --keep-dir \""; var_2; "\" -maxrregcount=0  --machine 64 -ptx -cudart static  -o \""; var_13; "\" \""; var_14; "\""|]
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
System.Console.WriteLine(var_33)
let (var_34: EnvHeap0) = ({mem_0 = (var_1: ManagedCuda.CudaContext)} : EnvHeap0)
let (var_35: uint64) = 1048576UL
let (var_36: ManagedCuda.CudaContext) = var_34.mem_0
let (var_37: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_35)
let (var_38: ManagedCuda.BasicTypes.CUdeviceptr) = var_36.AllocateMemory(var_37)
let (var_39: uint64) = uint64 var_38
let (var_40: (uint64 ref)) = (ref var_39)
let (var_41: ResizeArray<Env1>) = ResizeArray<Env1>()
let (var_42: EnvStack2) = EnvStack2((var_41: ResizeArray<Env1>))
let (var_43: ResizeArray<Env3>) = ResizeArray<Env3>()
let (var_44: EnvStack4) = EnvStack4((var_43: ResizeArray<Env3>))
let (var_45: EnvHeap5) = ({mem_0 = (var_42: EnvStack2); mem_1 = (var_40: (uint64 ref)); mem_2 = (var_35: uint64); mem_3 = (var_44: EnvStack4)} : EnvHeap5)
let (var_46: EnvHeap6) = ({mem_0 = (var_36: ManagedCuda.CudaContext); mem_1 = (var_45: EnvHeap5)} : EnvHeap6)
method_1((var_46: EnvHeap6), (var_32: ManagedCuda.BasicTypes.CUmodule))
let (var_47: ManagedCuda.CudaRand.GeneratorType) = ManagedCuda.CudaRand.GeneratorType.PseudoDefault
let (var_48: ManagedCuda.CudaRand.CudaRandDevice) = ManagedCuda.CudaRand.CudaRandDevice(var_47)
let (var_49: ManagedCuda.CudaContext) = var_46.mem_0
let (var_50: EnvHeap5) = var_46.mem_1
let (var_51: EnvHeap7) = ({mem_0 = (var_49: ManagedCuda.CudaContext); mem_1 = (var_48: ManagedCuda.CudaRand.CudaRandDevice); mem_2 = (var_50: EnvHeap5)} : EnvHeap7)
let (var_52: ManagedCuda.CudaBlas.PointerMode) = ManagedCuda.CudaBlas.PointerMode.Host
let (var_53: ManagedCuda.CudaBlas.AtomicsMode) = ManagedCuda.CudaBlas.AtomicsMode.Allowed
let (var_54: ManagedCuda.CudaBlas.CudaBlas) = ManagedCuda.CudaBlas.CudaBlas(var_52, var_53)
let (var_55: ManagedCuda.CudaContext) = var_51.mem_0
let (var_56: ManagedCuda.CudaRand.CudaRandDevice) = var_51.mem_1
let (var_57: EnvHeap5) = var_51.mem_2
let (var_58: EnvHeap8) = ({mem_0 = (var_55: ManagedCuda.CudaContext); mem_1 = (var_54: ManagedCuda.CudaBlas.CudaBlas); mem_2 = (var_56: ManagedCuda.CudaRand.CudaRandDevice); mem_3 = (var_57: EnvHeap5)} : EnvHeap8)
let (var_65: ResizeArray<Env9>) = ResizeArray<Env9>()
let (var_66: EnvStack10) = EnvStack10((var_65: ResizeArray<Env9>))
let (var_67: ManagedCuda.CudaContext) = var_58.mem_0
let (var_68: ManagedCuda.CudaBlas.CudaBlas) = var_58.mem_1
let (var_69: ManagedCuda.CudaRand.CudaRandDevice) = var_58.mem_2
let (var_70: EnvHeap5) = var_58.mem_3
let (var_71: EnvHeap11) = ({mem_0 = (var_67: ManagedCuda.CudaContext); mem_1 = (var_68: ManagedCuda.CudaBlas.CudaBlas); mem_2 = (var_69: ManagedCuda.CudaRand.CudaRandDevice); mem_3 = (var_66: EnvStack10); mem_4 = (var_70: EnvHeap5)} : EnvHeap11)
let (var_83: ResizeArray<Env12>) = ResizeArray<Env12>()
let (var_84: EnvStack13) = EnvStack13((var_83: ResizeArray<Env12>))
let (var_85: ManagedCuda.CudaContext) = var_71.mem_0
let (var_86: ManagedCuda.CudaBlas.CudaBlas) = var_71.mem_1
let (var_87: ManagedCuda.CudaRand.CudaRandDevice) = var_71.mem_2
let (var_88: EnvStack10) = var_71.mem_3
let (var_89: EnvHeap5) = var_71.mem_4
let (var_90: EnvHeap14) = ({mem_0 = (var_85: ManagedCuda.CudaContext); mem_1 = (var_86: ManagedCuda.CudaBlas.CudaBlas); mem_2 = (var_87: ManagedCuda.CudaRand.CudaRandDevice); mem_3 = (var_88: EnvStack10); mem_4 = (var_84: EnvStack13); mem_5 = (var_89: EnvHeap5)} : EnvHeap14)
let (var_91: (bool ref)) = (ref true)
let (var_92: ManagedCuda.CudaStream) = ManagedCuda.CudaStream()
let (var_93: ManagedCuda.CudaEvent) = ManagedCuda.CudaEvent()
let (var_94: EnvHeap15) = ({mem_0 = (var_93: ManagedCuda.CudaEvent); mem_1 = (var_91: (bool ref)); mem_2 = (var_92: ManagedCuda.CudaStream)} : EnvHeap15)
let (var_95: Env12) = method_7((var_94: EnvHeap15), (var_90: EnvHeap14), (var_32: ManagedCuda.BasicTypes.CUmodule))
let (var_96: (int64 ref)) = var_95.mem_0
let (var_97: Env16) = var_95.mem_1
let (var_98: EnvHeap15) = var_97.mem_0
let (var_99: ManagedCuda.CudaContext) = var_90.mem_0
let (var_100: ManagedCuda.CudaBlas.CudaBlas) = var_90.mem_1
let (var_101: ManagedCuda.CudaRand.CudaRandDevice) = var_90.mem_2
let (var_102: EnvStack10) = var_90.mem_3
let (var_103: EnvStack13) = var_90.mem_4
let (var_104: EnvHeap5) = var_90.mem_5
let (var_105: EnvHeap17) = ({mem_0 = (var_99: ManagedCuda.CudaContext); mem_1 = (var_100: ManagedCuda.CudaBlas.CudaBlas); mem_2 = (var_101: ManagedCuda.CudaRand.CudaRandDevice); mem_3 = (var_102: EnvStack10); mem_4 = (var_103: EnvStack13); mem_5 = (var_104: EnvHeap5); mem_6 = (var_96: (int64 ref)); mem_7 = (var_98: EnvHeap15)} : EnvHeap17)
let (var_106: EnvStack18) = method_9((var_105: EnvHeap17), (var_32: ManagedCuda.BasicTypes.CUmodule))
let (var_107: (int64 ref)) = var_106.mem_0
let (var_108: (uint64 ref)) = var_106.mem_1
let (var_109: int64) = 0L
let (var_110: int64) = 9L
let (var_111: int64) = 3L
let (var_112: int64) = 1L
let (var_113: int64) = 0L
let (var_114: int64) = 3L
let (var_115: int64) = 0L
let (var_116: int64) = 3L
let (var_117: int64) = 0L
let (var_118: int64) = 3L
method_19((var_105: EnvHeap17), (var_32: ManagedCuda.BasicTypes.CUmodule), (var_107: (int64 ref)), (var_108: (uint64 ref)), (var_109: int64), (var_110: int64), (var_111: int64), (var_112: int64), (var_113: int64), (var_114: int64), (var_115: int64), (var_116: int64), (var_117: int64), (var_118: int64))
let (var_119: EnvStack18) = method_31((var_107: (int64 ref)), (var_108: (uint64 ref)), (var_105: EnvHeap17), (var_32: ManagedCuda.BasicTypes.CUmodule))
let (var_120: (int64 ref)) = var_119.mem_0
let (var_121: (uint64 ref)) = var_119.mem_1
let (var_122: int64) = 0L
let (var_123: int64) = 9L
let (var_124: int64) = 3L
let (var_125: int64) = 1L
let (var_126: int64) = 0L
let (var_127: int64) = 3L
let (var_128: int64) = 0L
let (var_129: int64) = 3L
let (var_130: int64) = 0L
let (var_131: int64) = 3L
method_19((var_105: EnvHeap17), (var_32: ManagedCuda.BasicTypes.CUmodule), (var_120: (int64 ref)), (var_121: (uint64 ref)), (var_122: int64), (var_123: int64), (var_124: int64), (var_125: int64), (var_126: int64), (var_127: int64), (var_128: int64), (var_129: int64), (var_130: int64), (var_131: int64))
let (var_132: EnvStack19) = method_45((var_107: (int64 ref)), (var_108: (uint64 ref)), (var_120: (int64 ref)), (var_121: (uint64 ref)), (var_105: EnvHeap17), (var_32: ManagedCuda.BasicTypes.CUmodule))
let (var_133: (int64 ref)) = var_132.mem_0
let (var_134: (uint64 ref)) = var_132.mem_1
let (var_135: int64) = 0L
let (var_136: int64) = 3L
let (var_137: int64) = 1L
let (var_138: int64) = 0L
let (var_139: int64) = 3L
let (var_140: int64) = 0L
let (var_141: int64) = 3L
method_48((var_105: EnvHeap17), (var_32: ManagedCuda.BasicTypes.CUmodule), (var_133: (int64 ref)), (var_134: (uint64 ref)), (var_135: int64), (var_136: int64), (var_137: int64), (var_138: int64), (var_139: int64), (var_140: int64), (var_141: int64))
let (var_142: EnvStack19) = method_53((var_107: (int64 ref)), (var_108: (uint64 ref)), (var_120: (int64 ref)), (var_121: (uint64 ref)), (var_105: EnvHeap17), (var_32: ManagedCuda.BasicTypes.CUmodule))
let (var_143: (int64 ref)) = var_142.mem_0
let (var_144: (uint64 ref)) = var_142.mem_1
let (var_145: int64) = 0L
let (var_146: int64) = 3L
let (var_147: int64) = 1L
let (var_148: int64) = 0L
let (var_149: int64) = 3L
let (var_150: int64) = 0L
let (var_151: int64) = 3L
method_48((var_105: EnvHeap17), (var_32: ManagedCuda.BasicTypes.CUmodule), (var_143: (int64 ref)), (var_144: (uint64 ref)), (var_145: int64), (var_146: int64), (var_147: int64), (var_148: int64), (var_149: int64), (var_150: int64), (var_151: int64))
let (var_152: EnvStack19) = method_55((var_107: (int64 ref)), (var_108: (uint64 ref)), (var_120: (int64 ref)), (var_121: (uint64 ref)), (var_105: EnvHeap17), (var_32: ManagedCuda.BasicTypes.CUmodule))
let (var_153: (int64 ref)) = var_152.mem_0
let (var_154: (uint64 ref)) = var_152.mem_1
let (var_155: int64) = 0L
let (var_156: int64) = 3L
let (var_157: int64) = 1L
let (var_158: int64) = 0L
let (var_159: int64) = 3L
let (var_160: int64) = 0L
let (var_161: int64) = 3L
method_48((var_105: EnvHeap17), (var_32: ManagedCuda.BasicTypes.CUmodule), (var_153: (int64 ref)), (var_154: (uint64 ref)), (var_155: int64), (var_156: int64), (var_157: int64), (var_158: int64), (var_159: int64), (var_160: int64), (var_161: int64))
method_57((var_103: EnvStack13))
method_59((var_88: EnvStack10))
var_54.Dispose()
var_48.Dispose()
let (var_162: uint64) = method_5((var_40: (uint64 ref)))
let (var_163: ManagedCuda.BasicTypes.SizeT) = ManagedCuda.BasicTypes.SizeT(var_162)
let (var_164: ManagedCuda.BasicTypes.CUdeviceptr) = ManagedCuda.BasicTypes.CUdeviceptr(var_163)
var_49.FreeMemory(var_164)
var_40 := 0UL
var_1.Dispose()

