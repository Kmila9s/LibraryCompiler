-- =============================================================================
--   IEEE.std_logic_1164  —  STUB / declaration-only mirror
-- =============================================================================
--
-- Source-of-truth: IEEE Std 1164-1993 (multivalue logic system) public API.
-- This file is the synth pipeline's reference for which symbols come from
-- IEEE.std_logic_1164.all. It declares the types and function signatures
-- that user VHDL code may reference; bodies are intentionally empty / left
-- as black-box stubs because the synthesizer lowers each call to a native
-- netlist primitive in `Interpreter/Synthesis/IEEEPrimitives.cs` rather
-- than executing a body.
--
-- v1 scope (May 2026): name-registration only. The Interpreter's
-- IEEELibraryLoader reads this file once per process, registers the
-- function/type names in FunctionRegistry / TypeRegistry as black-box
-- stubs, and the synth keeps using its hard-coded primitive emitters.
--
-- v2 (queued): replace the empty bodies below with the canonical IEEE
-- source so the simulator can actually evaluate them line-by-line, which
-- removes the synth's reliance on per-function shims in StandardFunctions.cs.

package std_logic_1164 is

    -- Multivalue scalar logic.
    type std_ulogic is ('U', 'X', '0', '1', 'Z', 'W', 'L', 'H', '-');

    -- Resolved scalar (default in user code).
    subtype std_logic is std_ulogic;

    -- Unconstrained vector forms (size determined at instantiation).
    type std_ulogic_vector is array (natural range <>) of std_ulogic;
    type std_logic_vector  is array (natural range <>) of std_logic;

    -- ---- Logical operators (declarations; bodies = primitive cells) ----
    function "and"  (l, r : std_ulogic) return std_ulogic;
    function "or"   (l, r : std_ulogic) return std_ulogic;
    function "nand" (l, r : std_ulogic) return std_ulogic;
    function "nor"  (l, r : std_ulogic) return std_ulogic;
    function "xor"  (l, r : std_ulogic) return std_ulogic;
    function "xnor" (l, r : std_ulogic) return std_ulogic;
    function "not"  (l    : std_ulogic) return std_ulogic;

    -- Vector forms — same operators applied bit-wise.
    function "and"  (l, r : std_logic_vector) return std_logic_vector;
    function "or"   (l, r : std_logic_vector) return std_logic_vector;
    function "nand" (l, r : std_logic_vector) return std_logic_vector;
    function "nor"  (l, r : std_logic_vector) return std_logic_vector;
    function "xor"  (l, r : std_logic_vector) return std_logic_vector;
    function "xnor" (l, r : std_logic_vector) return std_logic_vector;
    function "not"  (l    : std_logic_vector) return std_logic_vector;

    -- ---- Edge-detection (clock recognition) ----
    function rising_edge  (signal s : std_ulogic) return boolean;
    function falling_edge (signal s : std_ulogic) return boolean;

    -- ---- Type coercion to clean 0/1/X ----
    function to_x01    (s : std_ulogic) return std_ulogic;
    function to_x01    (v : std_logic_vector) return std_logic_vector;
    function to_x01z   (s : std_ulogic) return std_ulogic;
    function to_ux01   (s : std_ulogic) return std_ulogic;

    -- ---- Predicates ----
    function is_x (s : std_ulogic) return boolean;
    function is_x (v : std_logic_vector) return boolean;

end package std_logic_1164;
