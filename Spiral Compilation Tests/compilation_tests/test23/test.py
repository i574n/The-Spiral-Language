kernel = r"""
template <typename el, int dim> struct static_array { el v[dim]; };
template <typename el, int dim, typename default_int> struct static_array_list { el v[dim]; default_int length; };
"""
class static_array(list):
    def __init__(self, length):
        for _ in range(length):
            self.append(None)

class static_array_list(static_array):
    def __init__(self, length):
        super().__init__(length)
        self.length = 0
from dataclasses import dataclass
from typing import NamedTuple, Union, Callable, Tuple
i8 = i16 = i32 = i64 = u8 = u16 = u32 = u64 = int; f32 = f64 = float; char = string = str

def main():
    // let v0 = false
    v0 = False
    // let v1 = true
    v1 = True
    del v1
    // let v2 = false
    v2 = False
    del v2
    // v0  = false
    v3 = v0 == False
    del v0, v3
    return 0

if __name__ == '__main__': print(main())
