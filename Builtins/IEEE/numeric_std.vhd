-- =============================================================================
--   IEEE.numeric_std  —  STUB / declaration-only mirror
-- =============================================================================
--
-- Source-of-truth: IEEE Std 1076.3-1997 (numeric_std) public API for signed
-- and unsigned integer arithmetic on bit vectors. This file is the synth
-- pipeline's reference for which symbols come from IEEE.numeric_std.all.
-- Bodies are intentionally empty stubs (see std_logic_1164.vhd preamble for
-- rationale).

library IEEE;
use IEEE.std_logic_1164.all;

package numeric_std is

    -- ---- Numeric vector types ----
    type unsigned is array (natural range <>) of std_logic;
    type signed   is array (natural range <>) of std_logic;

    -- ---- Conversions to/from integers ----
    function to_integer  (v : unsigned) return natural;
    function to_integer  (v : signed)   return integer;
    function to_unsigned (i : natural; width : natural) return unsigned;
    function to_signed   (i : integer; width : natural) return signed;

    -- ---- Width adjustment ----
    function resize (v : unsigned; new_width : natural) return unsigned;
    function resize (v : signed;   new_width : natural) return signed;

    -- ---- Arithmetic (return widest operand width unless noted) ----
    function "+" (l, r : unsigned) return unsigned;
    function "+" (l, r : signed)   return signed;
    function "+" (l : unsigned; r : natural) return unsigned;
    function "+" (l : signed;   r : integer) return signed;
    function "-" (l, r : unsigned) return unsigned;
    function "-" (l, r : signed)   return signed;
    function "-" (l    : signed)   return signed;     -- unary
    function "*" (l, r : unsigned) return unsigned;   -- result width = sum of operand widths
    function "*" (l, r : signed)   return signed;
    function "/" (l, r : unsigned) return unsigned;
    function "/" (l, r : signed)   return signed;
    function "rem" (l, r : unsigned) return unsigned;
    function "mod" (l, r : signed)   return signed;
    function "abs" (l    : signed)   return signed;

    -- ---- Comparisons ----
    function "="  (l, r : unsigned) return boolean;
    function "/=" (l, r : unsigned) return boolean;
    function "<"  (l, r : unsigned) return boolean;
    function "<=" (l, r : unsigned) return boolean;
    function ">"  (l, r : unsigned) return boolean;
    function ">=" (l, r : unsigned) return boolean;
    function "="  (l, r : signed)   return boolean;
    function "/=" (l, r : signed)   return boolean;
    function "<"  (l, r : signed)   return boolean;
    function "<=" (l, r : signed)   return boolean;
    function ">"  (l, r : signed)   return boolean;
    function ">=" (l, r : signed)   return boolean;

    -- ---- Logical-shift / rotate ----
    function shift_left   (v : unsigned; n : natural) return unsigned;
    function shift_left   (v : signed;   n : natural) return signed;
    function shift_right  (v : unsigned; n : natural) return unsigned;
    function shift_right  (v : signed;   n : natural) return signed;
    function rotate_left  (v : unsigned; n : natural) return unsigned;
    function rotate_left  (v : signed;   n : natural) return signed;
    function rotate_right (v : unsigned; n : natural) return unsigned;
    function rotate_right (v : signed;   n : natural) return signed;

    -- ---- Std-logic-vector bridges (legacy / synopsys-compat names that
    --      a lot of student VHDL still uses; aliased here so the same code
    --      compiles regardless of which library set was imported). ----
    function conv_integer         (v : unsigned)              return natural;
    function conv_integer         (v : signed)                return integer;
    function conv_std_logic_vector(i : integer; w : natural)  return std_logic_vector;

end package numeric_std;
