
(record_declaration name: (identifier) @name) @definition.class
(struct_declaration name: (identifier) @name) @definition.class
(enum_declaration name: (identifier) @name) @definition.enum

(invocation_expression
    function: (identifier) @name) @reference.call
(invocation_expression
    function: (generic_name (identifier) @name)) @reference.call
(invocation_expression
    function: (member_access_expression
        name: (identifier) @name)) @reference.call
(invocation_expression
    function: (member_access_expression
        name: (generic_name (identifier) @name))) @reference.call
(member_access_expression
    expression: (identifier) @name
    (#match? @name "^[A-Z]")) @reference.type
(member_access_expression
    expression: (member_access_expression
        name: (identifier) @name)
    (#match? @name "^[A-Z]")) @reference.type

(_ type: (identifier) @name) @reference.type
(_ type: (generic_name (identifier) @name)) @reference.type
(_ type: (qualified_name name: (identifier) @name)) @reference.type
(method_declaration returns: (identifier) @name) @reference.type
(method_declaration returns: (generic_name (identifier) @name)) @reference.type
(type_argument_list (identifier) @name) @reference.type
(base_list (identifier) @name) @reference.type
(base_list (generic_name (identifier) @name)) @reference.type
(attribute name: (identifier) @name) @reference.type
