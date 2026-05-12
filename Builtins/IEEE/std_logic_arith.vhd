-- =============================================================================
--   IEEE.std_logic_arith  —  STUB / declaration-only mirror (Synopsys legacy)
-- =============================================================================
--
-- Source-of-truth: Synopsys std_logic_arith. Predates IEEE.numeric_std and
-- is technically vendor extension territory, but most legacy classroom
-- examples still use it; users will hit "library not found" without it.
-- v1 scope: name-registration only.

package std_logic_arith is

    -- Vector forms with arithmetic semantics — duplicated by numeric_std,
    -- but kept here so legacy `use IEEE.std_logic_arith.all;` still resolves.
    type unsigned is array (natural range <>) of std_logic;
    type signed   is array (natural range <>) of std_logic;

    -- ---- Conversions ----
    function conv_integer        (arg : integer)          return integer;
    function conv_integer        (arg : unsigned)         return integer;
    function conv_integer        (arg : signed)           return integer;
    function conv_integer        (arg : std_logic_vector) return integer;
    function conv_integer        (arg : std_logic)        return integer;

    function conv_unsigned       (arg : integer;          size : integer) return unsigned;
    function conv_unsigned       (arg : unsigned;         size : integer) return unsigned;
    function conv_unsigned       (arg : signed;           size : integer) return unsigned;
    function conv_unsigned       (arg : std_logic_vector; size : integer) return unsigned;

    function conv_signed         (arg : integer;          size : integer) return signed;
    function conv_signed         (arg : unsigned;         size : integer) return signed;
    function conv_signed         (arg : signed;           size : integer) return signed;
    function conv_signed         (arg : std_logic_vector; size : integer) return signed;

    function conv_std_logic_vector (arg : integer;          size : integer) return std_logic_vector;
    function conv_std_logic_vector (arg : unsigned;         size : integer) return std_logic_vector;
    function conv_std_logic_vector (arg : signed;           size : integer) return std_logic_vector;
    function conv_std_logic_vector (arg : std_logic_vector; size : integer) return std_logic_vector;

    -- ---- Arithmetic operators (declarations only) ----
    function "+"  (l, r : unsigned) return unsigned;
    function "+"  (l, r : signed)   return signed;
    function "-"  (l, r : unsigned) return unsigned;
    function "-"  (l, r : signed)   return signed;
    function "*"  (l, r : unsigned) return unsigned;
    function "*"  (l, r : signed)   return signed;

    -- ---- Shifts ----
    function shl (arg : unsigned; count : unsigned) return unsigned;
    function shl (arg : signed;   count : unsigned) return signed;
    function shr (arg : unsigned; count : unsigned) return unsigned;
    function shr (arg : signed;   count : unsigned) return signed;

    -- ---- Comparisons ----
    function "<"  (l, r : unsigned) return boolean;
    function "<"  (l, r : signed)   return boolean;
    function ">"  (l, r : unsigned) return boolean;
    function ">"  (l, r : signed)   return boolean;
    function "<=" (l, r : unsigned) return boolean;
    function "<=" (l, r : signed)   return boolean;
    function ">=" (l, r : unsigned) return boolean;
    function ">=" (l, r : signed)   return boolean;

    -- ---- Predicates ----
    function ext  (arg : std_logic_vector; size : integer) return std_logic_vector;
    function sxt  (arg : std_logic_vector; size : integer) return std_logic_vector;

end package std_logic_arith;
