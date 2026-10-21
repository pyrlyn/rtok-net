using System.Runtime.InteropServices;

namespace Pyrlyn.TreeSitter.Native;

// The tree-sitter C API (lib/include/tree_sitter/api.h, tree-sitter 0.25) and the grammar
// entry points, all in one native library. NativeAOT links it statically (DirectPInvoke in
// build/Pyrlyn.TreeSitter.targets); the JIT loads the shared library beside the assembly.

internal static unsafe partial class NativeMethods
{
    internal const string Library = "pyrlyn_tree_sitter";

    internal const uint PredicateStepDone = 0;
    internal const uint PredicateStepCapture = 1;
    internal const uint PredicateStepString = 2;

    // Parser
    [LibraryImport(Library, EntryPoint = "ts_parser_new")]
    internal static partial nint ParserNew();

    [LibraryImport(Library, EntryPoint = "ts_parser_delete")]
    internal static partial void ParserDelete(nint parser);

    [LibraryImport(Library, EntryPoint = "ts_parser_set_language")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool ParserSetLanguage(nint parser, nint language);

    [LibraryImport(Library, EntryPoint = "ts_parser_parse_string")]
    internal static partial nint ParserParseString(
        nint parser,
        nint oldTree,
        byte* text,
        uint length
    );

    [LibraryImport(Library, EntryPoint = "ts_parser_reset")]
    internal static partial void ParserReset(nint parser);

    // Tree
    [LibraryImport(Library, EntryPoint = "ts_tree_delete")]
    internal static partial void TreeDelete(nint tree);

    [LibraryImport(Library, EntryPoint = "ts_tree_root_node")]
    internal static partial TSNode TreeRootNode(nint tree);

    // Node
    [LibraryImport(Library, EntryPoint = "ts_node_type")]
    internal static partial byte* NodeType(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_symbol")]
    internal static partial ushort NodeSymbol(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_start_byte")]
    internal static partial uint NodeStartByte(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_end_byte")]
    internal static partial uint NodeEndByte(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_start_point")]
    internal static partial TSPoint NodeStartPoint(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_end_point")]
    internal static partial TSPoint NodeEndPoint(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_is_null")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool NodeIsNull(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_is_named")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool NodeIsNamed(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_is_missing")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool NodeIsMissing(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_has_error")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool NodeHasError(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_is_error")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool NodeIsError(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_parent")]
    internal static partial TSNode NodeParent(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_child")]
    internal static partial TSNode NodeChild(TSNode node, uint index);

    [LibraryImport(Library, EntryPoint = "ts_node_child_count")]
    internal static partial uint NodeChildCount(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_named_child")]
    internal static partial TSNode NodeNamedChild(TSNode node, uint index);

    [LibraryImport(Library, EntryPoint = "ts_node_named_child_count")]
    internal static partial uint NodeNamedChildCount(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_child_by_field_name")]
    internal static partial TSNode NodeChildByFieldName(TSNode node, byte* name, uint length);

    [LibraryImport(Library, EntryPoint = "ts_node_next_sibling")]
    internal static partial TSNode NodeNextSibling(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_prev_sibling")]
    internal static partial TSNode NodePrevSibling(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_node_eq")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool NodeEq(TSNode a, TSNode b);

    // Tree cursor
    [LibraryImport(Library, EntryPoint = "ts_tree_cursor_new")]
    internal static partial TSTreeCursor TreeCursorNew(TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_tree_cursor_delete")]
    internal static partial void TreeCursorDelete(ref TSTreeCursor cursor);

    [LibraryImport(Library, EntryPoint = "ts_tree_cursor_current_node")]
    internal static partial TSNode TreeCursorCurrentNode(ref TSTreeCursor cursor);

    [LibraryImport(Library, EntryPoint = "ts_tree_cursor_goto_first_child")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool TreeCursorGotoFirstChild(ref TSTreeCursor cursor);

    [LibraryImport(Library, EntryPoint = "ts_tree_cursor_goto_next_sibling")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool TreeCursorGotoNextSibling(ref TSTreeCursor cursor);

    [LibraryImport(Library, EntryPoint = "ts_tree_cursor_goto_parent")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool TreeCursorGotoParent(ref TSTreeCursor cursor);

    // Query
    [LibraryImport(Library, EntryPoint = "ts_query_new")]
    internal static partial nint QueryNew(
        nint language,
        byte* source,
        uint length,
        uint* errorOffset,
        uint* errorType
    );

    [LibraryImport(Library, EntryPoint = "ts_query_delete")]
    internal static partial void QueryDelete(nint query);

    [LibraryImport(Library, EntryPoint = "ts_query_pattern_count")]
    internal static partial uint QueryPatternCount(nint query);

    [LibraryImport(Library, EntryPoint = "ts_query_capture_count")]
    internal static partial uint QueryCaptureCount(nint query);

    [LibraryImport(Library, EntryPoint = "ts_query_string_count")]
    internal static partial uint QueryStringCount(nint query);

    [LibraryImport(Library, EntryPoint = "ts_query_start_byte_for_pattern")]
    internal static partial uint QueryStartByteForPattern(nint query, uint patternIndex);

    [LibraryImport(Library, EntryPoint = "ts_query_predicates_for_pattern")]
    internal static partial TSQueryPredicateStep* QueryPredicatesForPattern(
        nint query,
        uint patternIndex,
        uint* stepCount
    );

    [LibraryImport(Library, EntryPoint = "ts_query_capture_name_for_id")]
    internal static partial byte* QueryCaptureNameForId(nint query, uint index, uint* length);

    [LibraryImport(Library, EntryPoint = "ts_query_string_value_for_id")]
    internal static partial byte* QueryStringValueForId(nint query, uint index, uint* length);

    // Query cursor
    [LibraryImport(Library, EntryPoint = "ts_query_cursor_new")]
    internal static partial nint QueryCursorNew();

    [LibraryImport(Library, EntryPoint = "ts_query_cursor_delete")]
    internal static partial void QueryCursorDelete(nint cursor);

    [LibraryImport(Library, EntryPoint = "ts_query_cursor_exec")]
    internal static partial void QueryCursorExec(nint cursor, nint query, TSNode node);

    [LibraryImport(Library, EntryPoint = "ts_query_cursor_next_match")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool QueryCursorNextMatch(nint cursor, TSQueryMatch* match);

    [LibraryImport(Library, EntryPoint = "ts_query_cursor_did_exceed_match_limit")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool QueryCursorDidExceedMatchLimit(nint cursor);

    [LibraryImport(Library, EntryPoint = "ts_query_cursor_match_limit")]
    internal static partial uint QueryCursorMatchLimit(nint cursor);

    [LibraryImport(Library, EntryPoint = "ts_query_cursor_set_match_limit")]
    internal static partial void QueryCursorSetMatchLimit(nint cursor, uint limit);

    [LibraryImport(Library, EntryPoint = "ts_query_cursor_set_byte_range")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool QueryCursorSetByteRange(nint cursor, uint start, uint end);

    // Language
    [LibraryImport(Library, EntryPoint = "ts_language_abi_version")]
    internal static partial uint LanguageAbiVersion(nint language);

    [LibraryImport(Library, EntryPoint = "ts_language_symbol_count")]
    internal static partial uint LanguageSymbolCount(nint language);

    // Grammars
    [LibraryImport(Library, EntryPoint = "tree_sitter_c")]
    internal static partial nint GrammarC();

    [LibraryImport(Library, EntryPoint = "tree_sitter_c_sharp")]
    internal static partial nint GrammarCSharp();

    [LibraryImport(Library, EntryPoint = "tree_sitter_dart")]
    internal static partial nint GrammarDart();

    [LibraryImport(Library, EntryPoint = "tree_sitter_go")]
    internal static partial nint GrammarGo();

    [LibraryImport(Library, EntryPoint = "tree_sitter_java")]
    internal static partial nint GrammarJava();

    [LibraryImport(Library, EntryPoint = "tree_sitter_javascript")]
    internal static partial nint GrammarJavaScript();

    [LibraryImport(Library, EntryPoint = "tree_sitter_kotlin")]
    internal static partial nint GrammarKotlin();

    [LibraryImport(Library, EntryPoint = "tree_sitter_php")]
    internal static partial nint GrammarPhp();

    [LibraryImport(Library, EntryPoint = "tree_sitter_python")]
    internal static partial nint GrammarPython();

    [LibraryImport(Library, EntryPoint = "tree_sitter_ruby")]
    internal static partial nint GrammarRuby();

    [LibraryImport(Library, EntryPoint = "tree_sitter_rust")]
    internal static partial nint GrammarRust();

    [LibraryImport(Library, EntryPoint = "tree_sitter_swift")]
    internal static partial nint GrammarSwift();

    [LibraryImport(Library, EntryPoint = "tree_sitter_tsx")]
    internal static partial nint GrammarTsx();

    [LibraryImport(Library, EntryPoint = "tree_sitter_typescript")]
    internal static partial nint GrammarTypeScript();
}
