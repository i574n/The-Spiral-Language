kernel = """
#pragma warning(disable: 4101 4065 4060)
// Add these as extra argument to the compiler to suppress the rest:
// --diag-suppress 186 --diag-suppress 177 --diag-suppress 550
#include <cstdint>
#include <array>
extern "C" __global__ void entry0() {
    int v0[1l];
    return ;
}
extern "C" __global__ void entry1() {
    int v0[2l];
    return ;
}
extern "C" __global__ void entry2() {
    int v0[3l];
    return ;
}
"""
import numpy as np
from dataclasses import dataclass
from typing import NamedTuple, Union, Callable, Tuple
i8 = i16 = i32 = i64 = u8 = u16 = u32 = u64 = int; f32 = f64 = float; char = string = str

def main():
    v0 = 0
    del v0
    v1 = 1
    del v1
    v2 = 2
    del v2
    return 

if __name__ == '__main__': print(main())
