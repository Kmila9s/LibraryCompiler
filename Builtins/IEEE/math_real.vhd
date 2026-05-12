-- =============================================================================
--   IEEE.math_real  —  STUB / declaration-only mirror
-- =============================================================================
--
-- Source-of-truth: IEEE Std 1076.2 (math_real package) public API.
-- v1 scope: name-registration only. The synth path lowers to primitive
-- cells (LOG, EXP, SIN, …) when emitters land; today these resolve as
-- black-box stubs that surface a "real-arithmetic synthesis not yet
-- supported" diagnostic rather than parsing as unknown identifiers.

package math_real is

    -- ---- Constants the standard exposes ----
    constant math_pi      : real := 3.14159_26535_89793;
    constant math_e       : real := 2.71828_18284_59045;
    constant math_log_of_2: real := 0.69314_71805_59945;

    -- ---- Conversions ----
    function ceil       (x : real) return real;
    function floor      (x : real) return real;
    function round      (x : real) return real;
    function trunc      (x : real) return real;
    function realmax    (x, y : real) return real;
    function realmin    (x, y : real) return real;

    -- ---- Powers, roots, logs ----
    function sqrt       (x : real) return real;
    function cbrt       (x : real) return real;
    function "**"       (x : real; y : integer) return real;
    function "**"       (x, y : real) return real;
    function exp        (x : real) return real;
    function log        (x : real) return real;
    function log        (x, base : real) return real;
    function log2       (x : real) return real;
    function log10      (x : real) return real;

    -- ---- Trigonometry ----
    function sin        (x : real) return real;
    function cos        (x : real) return real;
    function tan        (x : real) return real;
    function arcsin     (x : real) return real;
    function arccos     (x : real) return real;
    function arctan     (x : real) return real;
    function arctan     (y, x : real) return real;
    function sinh       (x : real) return real;
    function cosh       (x : real) return real;
    function tanh       (x : real) return real;
    function arcsinh    (x : real) return real;
    function arccosh    (x : real) return real;
    function arctanh    (x : real) return real;

end package math_real;
