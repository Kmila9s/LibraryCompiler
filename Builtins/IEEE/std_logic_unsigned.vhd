-- =============================================================================
--   IEEE.std_logic_unsigned  —  STUB / declaration-only mirror (Synopsys)
-- =============================================================================
--
-- Companion to std_logic_arith. Treats std_logic_vector as unsigned for
-- arithmetic and comparison operators. Kept as its own package so legacy
-- `use IEEE.std_logic_unsigned.all;` resolves.

package std_logic_unsigned is

    -- ---- Arithmetic on std_logic_vector (unsigned semantics) ----
    function "+" (l : std_logic_vector; r : std_logic_vector) return std_logic_vector;
    function "+" (l : std_logic_vector; r : integer)          return std_logic_vector;
    function "+" (l : integer;          r : std_logic_vector) return std_logic_vector;
    function "+" (l : std_logic_vector; r : std_logic)        return std_logic_vector;
    function "+" (l : std_logic;        r : std_logic_vector) return std_logic_vector;

    function "-" (l : std_logic_vector; r : std_logic_vector) return std_logic_vector;
    function "-" (l : std_logic_vector; r : integer)          return std_logic_vector;
    function "-" (l : integer;          r : std_logic_vector) return std_logic_vector;
    function "-" (l : std_logic_vector; r : std_logic)        return std_logic_vector;
    function "-" (l : std_logic;        r : std_logic_vector) return std_logic_vector;

    function "*" (l, r : std_logic_vector) return std_logic_vector;

    -- ---- Comparisons (unsigned semantics on std_logic_vector) ----
    function "<"  (l, r : std_logic_vector) return boolean;
    function ">"  (l, r : std_logic_vector) return boolean;
    function "<=" (l, r : std_logic_vector) return boolean;
    function ">=" (l, r : std_logic_vector) return boolean;
    function "="  (l, r : std_logic_vector) return boolean;
    function "/=" (l, r : std_logic_vector) return boolean;

    -- ---- Conversion helpers ----
    function conv_integer (arg : std_logic_vector) return integer;
    function shl          (arg : std_logic_vector; count : std_logic_vector) return std_logic_vector;
    function shr          (arg : std_logic_vector; count : std_logic_vector) return std_logic_vector;

end package std_logic_unsigned;
