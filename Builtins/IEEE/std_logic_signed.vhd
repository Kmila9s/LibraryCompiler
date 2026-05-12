-- =============================================================================
--   IEEE.std_logic_signed  —  STUB / declaration-only mirror (Synopsys)
-- =============================================================================
--
-- Companion to std_logic_arith. Same shape as std_logic_unsigned but with
-- signed arithmetic / comparison semantics. Kept as its own package so
-- legacy `use IEEE.std_logic_signed.all;` resolves.

package std_logic_signed is

    -- ---- Arithmetic on std_logic_vector (signed semantics) ----
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
    function "-" (l    : std_logic_vector) return std_logic_vector;
    function abs (l    : std_logic_vector) return std_logic_vector;

    -- ---- Comparisons (signed semantics on std_logic_vector) ----
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

end package std_logic_signed;
