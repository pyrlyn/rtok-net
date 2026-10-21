
(call_expression
    function: (identifier) @name) @reference.call
(call_expression
    function: (member_expression
        property: (property_identifier) @name)) @reference.call
(call_expression
    function: (member_expression
        object: (member_expression
            property: (property_identifier) @name))) @reference.call
(new_expression
    constructor: (member_expression
        property: (property_identifier) @name)) @reference.class
(type_identifier) @name @reference.type
(nested_type_identifier
    module: (identifier) @name) @reference.type
