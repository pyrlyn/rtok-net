
(call_expression
    function: (scoped_identifier
        name: (identifier) @name)) @reference.call

(use_declaration argument: (identifier) @name) @reference.import
(use_declaration argument: (scoped_identifier) @name) @reference.import
(use_declaration argument: (scoped_identifier name: (identifier) @name)) @reference.import
(use_declaration argument: (use_as_clause) @name) @reference.import
(use_declaration argument: (use_as_clause path: (identifier) @name)) @reference.import
(use_declaration argument: (use_as_clause path: (scoped_identifier name: (identifier) @name))) @reference.import
(use_declaration argument: (use_list) @name) @reference.import
(use_declaration argument: (scoped_use_list) @name) @reference.import
(use_list (identifier) @name) @reference.import
(use_list (scoped_identifier name: (identifier) @name)) @reference.import
(use_list (use_as_clause path: (identifier) @name)) @reference.import
(use_list (use_as_clause path: (scoped_identifier name: (identifier) @name))) @reference.import
(use_declaration argument: (use_wildcard) @name) @reference.import
(use_wildcard (identifier) @name) @reference.import
(use_wildcard (scoped_identifier name: (identifier) @name)) @reference.import

(type_identifier) @name @reference.type
(scoped_type_identifier
    path: (identifier) @name) @reference.type
(scoped_identifier
    name: (identifier) @name) @reference.call
(scoped_identifier
    path: (identifier) @name) @reference.call
(token_tree
    (identifier) @name @reference.call
    .
    (token_tree))
