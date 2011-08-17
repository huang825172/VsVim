﻿using System;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Vim.Extensions;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    internal static class VimUtil
    {
        internal static ICommonOperations CreateCommonOperations(
            ITextView textView,
            IOutliningManager outlining = null,
            IStatusUtil statusUtil = null,
            ISearchService searchService = null,
            IUndoRedoOperations undoRedoOperations = null,
            IVimData vimData = null,
            IVimHost vimHost = null,
            IClipboardDevice clipboardDevice = null,
            IFoldManager foldManager = null,
            IWordUtil wordUtil = null,
            IVimLocalSettings localSettings = null)
        {
            var editorOperations = EditorUtil.GetEditorOperations(textView);
            var editorOptions = EditorUtil.FactoryService.EditorOptionsFactory.GetOptions(textView);
            var jumpList = new JumpList(new BufferTrackingService());
            var keyMap = new KeyMap();
            localSettings = localSettings ?? CreateLocalSettings();
            wordUtil = wordUtil ?? GetWordUtil(textView);
            statusUtil = statusUtil ?? new StatusUtil();
            foldManager = foldManager ?? new FoldManager(
                textView,
                new FoldData(textView.TextBuffer),
                statusUtil,
                FSharpOption.Create(EditorUtil.FactoryService.OutliningManagerService.GetOutliningManager(textView)));
            searchService = searchService ?? CreateSearchService(localSettings.GlobalSettings);
            undoRedoOperations = undoRedoOperations ??
                                 new UndoRedoOperations(statusUtil, FSharpOption<ITextUndoHistory>.None, editorOperations);
            vimData = vimData ?? new VimData();
            vimHost = vimHost ?? new MockVimHost();
            clipboardDevice = clipboardDevice ?? new MockClipboardDevice();
            var operationsData = new OperationsData(
                editorOperations,
                editorOptions,
                foldManager,
                jumpList,
                keyMap,
                localSettings,
                outlining != null ? FSharpOption.Create(outlining) : FSharpOption<IOutliningManager>.None,
                CreateRegisterMap(clipboardDevice),
                searchService,
                statusUtil,
                textView,
                undoRedoOperations,
                vimData,
                vimHost,
                wordUtil);
            return new CommonOperations(operationsData);
        }

        internal static IMotionUtil CreateTextViewMotionUtil(
            ITextView textView,
            IMarkMap markMap = null,
            ISearchService search = null,
            ITextStructureNavigator navigator = null,
            IJumpList jumpList = null,
            IStatusUtil statusUtil = null,
            IVimData vimData = null,
            IEditorOptions editorOptions = null,
            IWordUtil wordUtil = null)
        {
            var localSettings = CreateLocalSettings();
            markMap = markMap ?? new MarkMap(new BufferTrackingService());
            search = search ?? CreateSearchService(localSettings.GlobalSettings);
            navigator = navigator ?? CreateTextStructureNavigator(textView, WordKind.NormalWord);
            jumpList = jumpList ?? CreateJumpList();
            statusUtil = statusUtil ?? new StatusUtil();
            wordUtil = wordUtil ?? GetWordUtil(textView);
            vimData = vimData ?? new VimData();
            return new MotionUtil(
                textView,
                markMap,
                localSettings,
                search,
                navigator,
                jumpList,
                statusUtil,
                wordUtil,
                vimData);
        }

        /// <summary>
        /// Create the IVimLocalSettings for the given ITextBuffer
        /// </summary>
        internal static IVimLocalSettings CreateLocalSettings(IVimGlobalSettings globalSettings = null)
        {
            globalSettings = globalSettings ?? new GlobalSettings();
            return new LocalSettings(globalSettings);
        }

        internal static IJumpList CreateJumpList(IBufferTrackingService bufferTrackingService = null)
        {
            bufferTrackingService = bufferTrackingService ?? new BufferTrackingService();
            return new JumpList(bufferTrackingService);
        }

        internal static RegisterMap CreateRegisterMap(IClipboardDevice device)
        {
            return CreateRegisterMap(device, () => null);
        }

        internal static CommandUtil CreateCommandUtil(
            ITextView textView,
            ICommonOperations operations = null,
            IMotionUtil motionUtil = null,
            IStatusUtil statusUtil = null,
            IRegisterMap registerMap = null,
            IMarkMap markMap = null,
            IVimData vimData = null,
            IUndoRedoOperations undoRedOperations = null,
            ISmartIndentationService smartIndentationService = null,
            IFoldManager foldManager = null,
            IVimHost vimHost = null,
            IMacroRecorder recorder = null,
            ISearchService searchService = null,
            ITextStructureNavigator wordNavigator = null,
            IVimLocalSettings localSettings = null)
        {
            statusUtil = statusUtil ?? new StatusUtil();
            undoRedOperations = undoRedOperations ?? VimUtil.CreateUndoRedoOperations(statusUtil);
            registerMap = registerMap ?? CreateRegisterMap(MockObjectFactory.CreateClipboardDevice().Object);
            markMap = markMap ?? new MarkMap(new BufferTrackingService());
            vimData = vimData ?? new VimData();
            motionUtil = motionUtil ?? CreateTextViewMotionUtil(textView, markMap: markMap, vimData: vimData);
            operations = operations ?? CreateCommonOperations(textView, vimData: vimData, statusUtil: statusUtil);
            smartIndentationService = smartIndentationService ?? CreateSmartIndentationService();
            foldManager = foldManager ?? EditorUtil.FactoryService.FoldManagerFactory.GetFoldManager(textView);
            wordNavigator = wordNavigator ?? CreateTextStructureNavigator(textView, WordKind.NormalWord);
            searchService = searchService ?? CreateSearchService();
            localSettings = localSettings ?? CreateLocalSettings();
            vimHost = vimHost ?? new MockVimHost();
            var vim = MockObjectFactory.CreateVim(
                registerMap: registerMap,
                map: markMap,
                host: vimHost,
                vimData: vimData,
                recorder: recorder,
                searchService: searchService);
            var vimTextBuffer = CreateVimTextBuffer(
                textView.TextBuffer,
                wordNavigator: wordNavigator,
                localSettings: localSettings,
                vim: vim.Object);
            var bufferData = CreateVimBufferData(
                vimTextBuffer,
                textView,
                statusUtil,
                undoRedOperations);
            var insertUtil = new InsertUtil(bufferData, operations);
            return new CommandUtil(
                bufferData,
                motionUtil,
                operations,
                smartIndentationService,
                foldManager,
                wordNavigator,
                insertUtil);
        }

        public static IVimTextBuffer CreateVimTextBuffer(
            ITextBuffer textBuffer,
            IVimLocalSettings localSettings = null,
            IJumpList jumpList = null,
            ITextStructureNavigator wordNavigator = null,
            IVim vim = null)
        {
            localSettings = localSettings ?? CreateLocalSettings();
            jumpList = jumpList ?? CreateJumpList();
            wordNavigator = wordNavigator ?? CreateTextStructureNavigator(textBuffer, WordKind.NormalWord);
            return new VimTextBuffer(
                textBuffer,
                localSettings,
                jumpList,
                wordNavigator,
                new BufferTrackingService(),
                vim);
        }

        /// <summary>
        /// Create a new instance of VimBufferData.  Centralized here to make it easier to 
        /// absorb API changes in the Unit Tests
        /// </summary>
        public static VimBufferData CreateVimBufferData(
            IVimTextBuffer vimTextBuffer,
            ITextView textView,
            IStatusUtil statusUtil,
            IUndoRedoOperations undoRedoOperations)
        {
            return new VimBufferData(
                textView,
                statusUtil,
                undoRedoOperations,
                vimTextBuffer);
        }

        internal static ISmartIndentationService CreateSmartIndentationService()
        {
            return EditorUtil.FactoryService.SmartIndentationService;
        }

        internal static UndoRedoOperations CreateUndoRedoOperations(IStatusUtil statusUtil = null)
        {
            statusUtil = statusUtil ?? new StatusUtil();
            return new UndoRedoOperations(statusUtil, FSharpOption<ITextUndoHistory>.None, null);
        }

        internal static RegisterMap CreateRegisterMap(IClipboardDevice device, Func<string> func)
        {
            Func<FSharpOption<string>> func2 = () =>
            {
                var result = func();
                return string.IsNullOrEmpty(result) ? FSharpOption<string>.None : FSharpOption.Create(result);
            };
            return new RegisterMap(device, func2.ToFSharpFunc());
        }

        internal static ISearchService CreateSearchService(IVimGlobalSettings settings = null)
        {
            settings = settings ?? new GlobalSettings();
            return new SearchService(EditorUtil.FactoryService.TextSearchService, settings);
        }

        internal static CommandBinding CreateNormalBinding(string name)
        {
            return CreateNormalBinding(name, () => CommandResult.NewCompleted(ModeSwitch.NoSwitch));
        }

        internal static CommandBinding CreateNormalBinding(string name, Action del)
        {
            return CreateNormalBinding(
                name,
                unused =>
                {
                    del();
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                });
        }

        internal static CommandBinding CreateNormalBinding(string name, Func<CommandData, CommandResult> func)
        {
            var fsharpFunc = func.ToFSharpFunc();
            var list = name.Select(KeyInputUtil.CharToKeyInput).ToFSharpList();
            var commandName = KeyInputSet.NewManyKeyInputs(list);
            var command = NormalCommand.NewPing(new PingData(fsharpFunc));
            return CommandBinding.NewNormalBinding(commandName, CommandFlags.None, command);
        }

        internal static ITextStructureNavigator CreateTextStructureNavigator(ITextBuffer textBuffer, WordKind kind)
        {
            var textView = EditorUtil.FactoryService.TextEditorFactory.CreateTextView(textBuffer);
            return CreateTextStructureNavigator(textView, kind);
        }

        internal static ITextStructureNavigator CreateTextStructureNavigator(ITextView textView, WordKind kind)
        {
            return GetWordUtil(textView).CreateTextStructureNavigator(kind);
        }

        internal static IWordUtil GetWordUtil(ITextView textView)
        {
            return EditorUtil.FactoryService.WordUtilFactory.GetWordUtil(textView.TextBuffer);
        }

        internal static CommandRunData CreateCommandRunData(
            Command command = null,
            CommandBinding binding = null,
            CommandResult result = null,
            CommandFlags flags = CommandFlags.None)
        {
            command = command ?? CreateNormalCommand();
            binding = binding ?? CreateNormalBinding(flags: flags);
            result = result ?? CommandResult.NewCompleted(ModeSwitch.NoSwitch);
            return new CommandRunData(binding, command, result);
        }

        internal static CommandBinding CreateNormalBinding(
            string name = "default",
            CommandFlags flags = CommandFlags.None,
            NormalCommand command = null)
        {
            command = command ?? NormalCommand.NewPutAfterCaret(false);
            return CommandBinding.NewNormalBinding(KeyNotationUtil.StringToKeyInputSet(name), flags, command);
        }

        internal static CommandBinding CreateMotionBinding(
            string name,
            Func<MotionData, NormalCommand> func)
        {
            return CreateMotionBinding(name, CommandFlags.None, func);
        }

        internal static CommandBinding CreateMotionBinding(
            string name = "default",
            CommandFlags flags = CommandFlags.None,
            Func<MotionData, NormalCommand> func = null)
        {
            func = func ?? NormalCommand.NewYank;
            return CommandBinding.NewMotionBinding(KeyNotationUtil.StringToKeyInputSet(name), flags, func.ToFSharpFunc());
        }

        internal static CommandBinding CreateComplexNormalBinding(
            string name,
            Action<KeyInput> action,
            CommandFlags flags = CommandFlags.None)
        {
            Func<KeyInput, BindResult<NormalCommand>> func = keyInput =>
            {
                action(keyInput);
                return BindResult<NormalCommand>.NewComplete(NormalCommand.NewPutAfterCaret(false));
            };

            var bindData = new BindData<NormalCommand>(
                FSharpOption<KeyRemapMode>.None,
                func.ToFSharpFunc());
            var bindDataStorage = BindDataStorage<NormalCommand>.NewSimple(bindData);
            return CommandBinding.NewComplexNormalBinding(
                KeyNotationUtil.StringToKeyInputSet(name),
                flags,
                bindDataStorage);
        }

        internal static CommandBinding CreateComplexNormalBinding(
            string name,
            Func<KeyInput, bool> predicate,
            KeyRemapMode remapMode = null,
            CommandFlags flags = CommandFlags.None)
        {
            var remapModeOption = FSharpOption.CreateForReference(remapMode);
            Func<KeyInput, BindResult<NormalCommand>> func = null;
            func = keyInput =>
            {
                if (predicate(keyInput))
                {
                    var data = new BindData<NormalCommand>(
                        remapModeOption,
                        func.ToFSharpFunc());
                    return BindResult<NormalCommand>.NewNeedMoreInput(data);
                }

                return BindResult<NormalCommand>.NewComplete(NormalCommand.NewPutAfterCaret(false));
            };

            var bindData = new BindData<NormalCommand>(
                remapModeOption,
                func.ToFSharpFunc());
            var bindDataStorage = BindDataStorage<NormalCommand>.NewSimple(bindData);
            return CommandBinding.NewComplexNormalBinding(
                KeyNotationUtil.StringToKeyInputSet(name),
                flags,
                bindDataStorage);
        }


        internal static Command CreateNormalCommand(
            NormalCommand command = null,
            CommandData commandData = null)
        {
            command = command ?? NormalCommand.NewPutAfterCaret(false);
            commandData = commandData ?? new CommandData(FSharpOption<int>.None, FSharpOption<RegisterName>.None);
            return Command.NewNormalCommand(command, commandData);
        }


        internal static MotionData CreateMotionData(
            Motion motion,
            int count)
        {
            return CreateMotionData(motion, new MotionArgument(MotionContext.AfterOperator, FSharpOption.Create(count), FSharpOption<int>.None));
        }

        internal static MotionData CreateMotionData(
            Motion motion,
            MotionArgument argument = null)
        {
            argument = argument ?? new MotionArgument(MotionContext.AfterOperator, FSharpOption<int>.None, FSharpOption<int>.None);
            return new MotionData(motion, argument);
        }

        internal static KeyInput CreateKeyInput(
            VimKey key = VimKey.None,
            KeyModifiers mod = KeyModifiers.None,
            char? c = null)
        {
            return new KeyInput(
                key,
                mod,
                c.HasValue ? FSharpOption<char>.Some(c.Value) : FSharpOption<char>.None);
        }

        internal static PatternData CreatePatternData(
            string pattern,
            Path path = null)
        {
            path = path ?? Path.Forward;
            return new PatternData(pattern, path);
        }

        internal static SearchData CreateSearchData(
            string pattern,
            SearchKind kind = null,
            SearchOptions options = SearchOptions.None)
        {
            kind = kind ?? SearchKind.Forward;
            return new SearchData(pattern, kind, options);
        }

        internal static ModeArgument CreateSubstituteArgument(
            SnapshotSpan span,
            SnapshotLineRange range = null,
            SubstituteData data = null)
        {
            range = range ?? SnapshotLineRangeUtil.CreateForSnapshot(span.Snapshot);
            data = data ?? new SubstituteData("a", "b", SubstituteFlags.None);
            return ModeArgument.NewSubstitute(span, range, data);
        }

        internal static ModeArgument CreateSubstituteArgument(
            SnapshotSpan span,
            string search,
            string replace,
            SubstituteFlags? flags = null,
            SnapshotLineRange range = null)
        {
            range = range ?? SnapshotLineRangeUtil.CreateForSnapshot(span.Snapshot);
            var data = new SubstituteData(search, replace, flags ?? SubstituteFlags.None);
            return ModeArgument.NewSubstitute(span, range, data);
        }

        internal static IIncrementalSearch CreateIncrementalSearch(
            ITextView textView,
            IVimLocalSettings settings,
            IVimData vimData,
            ISearchService search = null,
            IOutliningManager outliningManager = null,
            IStatusUtil statusUtil = null)
        {
            vimData = vimData ?? new VimData();
            search = search ?? CreateSearchService(settings.GlobalSettings);
            statusUtil = statusUtil ?? new StatusUtil();
            var nav = CreateTextStructureNavigator(textView, WordKind.NormalWord);
            var operations = CreateCommonOperations(
                textView: textView,
                outlining: outliningManager,
                vimData: vimData,
                searchService: search);
            return new IncrementalSearch(
                operations,
                settings,
                nav,
                statusUtil,
                vimData);
        }

        internal static MotionResult CreateMotionResult(
            SnapshotSpan span,
            bool isForward = true,
            MotionKind motionKind = null,
            MotionResultFlags flags = MotionResultFlags.None)
        {
            motionKind = motionKind ?? MotionKind.CharacterWiseInclusive;
            return new MotionResult(span, isForward, motionKind, flags);
        }

        internal static CommandData CreateCommandData(
            int? count = null,
            RegisterName name = null)
        {
            return new CommandData(
                FSharpOption.CreateForNullable(count),
                FSharpOption.CreateForReference(name));
        }

        internal static NormalCommand CreatePing(Action<CommandData> action)
        {
            Func<CommandData, CommandResult> func =
                commandData =>
                {
                    action(commandData);
                    return CommandResult.NewCompleted(ModeSwitch.NoSwitch);
                };
            var data = new PingData(func.ToFSharpFunc());
            return NormalCommand.NewPing(data);
        }

        internal static BindData<T> CreateBindData<T>(Func<KeyInput, BindResult<T>> func = null, KeyRemapMode remapMode = null)
        {
            func = func ?? (x => BindResult<T>.Cancelled);
            return new BindData<T>(FSharpOption.CreateForReference(remapMode), func.ToFSharpFunc());
        }

        internal static BindDataStorage<T> CreateBindDataStorage<T>(BindData<T> bindData = null)
        {
            bindData = bindData ?? CreateBindData<T>();
            return BindDataStorage<T>.NewSimple(bindData);
        }

        internal static StringData CreateStringDataBlock(params string[] values)
        {
            return StringData.NewBlock(NonEmptyCollectionUtil.OfSeq(values).Value);
        }
    }
}
