
(import_statement source: (string) @name) @reference.import
(import_specifier name: (identifier) @name) @reference.import
(call_expression
    function: (identifier) @doc
    arguments: (arguments (string) @name)
    (#eq? @doc "require")) @reference.import
