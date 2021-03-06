﻿using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class IncrementalSearchTest
    {
        private static SearchOptions s_options = SearchOptions.ConsiderIgnoreCase | SearchOptions.ConsiderSmartCase;
        private MockRepository _factory;
        private Mock<IVimData> _vimData;
        private Mock<ISearchService> _searchService;
        private Mock<ITextStructureNavigator> _nav;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IVimLocalSettings> _settings;
        private Mock<ICommonOperations> _operations;
        private Mock<IStatusUtil> _statusUtil;
        private ITextView _textView;
        private IncrementalSearch _searchRaw;
        private IIncrementalSearch _search;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _vimData = _factory.Create<IVimData>();
            _searchService = _factory.Create<ISearchService>();
            _nav = _factory.Create<ITextStructureNavigator>();
            _globalSettings = MockObjectFactory.CreateGlobalSettings(ignoreCase: true);
            _settings = MockObjectFactory.CreateLocalSettings(_globalSettings.Object);
            _statusUtil = _factory.Create<IStatusUtil>();
            _operations = _factory.Create<ICommonOperations>();
            _operations.SetupGet(x => x.TextView).Returns(_textView);
            _operations.Setup(x => x.EnsureCaretOnScreenAndTextExpanded());
            _operations.Setup(x => x.EnsurePointOnScreenAndTextExpanded(It.IsAny<SnapshotPoint>()));
            _searchRaw = new IncrementalSearch(
                _operations.Object,
                _settings.Object,
                _nav.Object,
                _searchService.Object,
                _statusUtil.Object,
                _vimData.Object);
            _search = _searchRaw;
        }

        private void ProcessWithEnter(string value)
        {
            _search.Begin(SearchKind.ForwardWithWrap);
            foreach (var cur in value)
            {
                var ki = KeyInputUtil.CharToKeyInput(cur);
                Assert.IsTrue(_search.Process(ki).IsSearchNeedMore);
            }
            Assert.IsTrue(_search.Process(KeyInputUtil.EnterKey).IsSearchComplete);
        }

        [Test]
        public void Process1()
        {
            Create("foo bar");
            var data = new SearchData(SearchText.NewPattern("b"), SearchKind.ForwardWithWrap, s_options);
            _search.Begin(SearchKind.ForwardWithWrap);
            _searchService
                .Setup(x => x.FindNext(data, _textView.GetCaretPoint().Add(1), _nav.Object))
                .Returns(FSharpOption.Create(_textView.GetLineRange(0).Extent));
            Assert.IsTrue(_search.Process(KeyInputUtil.CharToKeyInput('b')).IsSearchNeedMore);
        }

        [Test]
        public void Process2()
        {
            Create("foo bar");
            _vimData.SetupSet(x => x.LastSearchData = new SearchData(SearchText.NewPattern(""), SearchKind.ForwardWithWrap, s_options)).Verifiable();
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(_search.Process(KeyInputUtil.EnterKey).IsSearchComplete);
            _factory.Verify();
        }

        [Test]
        public void Process3()
        {
            Create("foo bar");
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(_search.Process(KeyInputUtil.EscapeKey).IsSearchCancelled);
        }

        [Test]
        public void LastSearch1()
        {
            Create("foo bar");
            var data = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, s_options);
            _vimData.SetupSet(x => x.LastSearchData = data).Verifiable();
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None)
                .Verifiable();
            ProcessWithEnter("foo");
            _factory.Verify();
        }

        [Test]
        public void LastSearch2()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None)
                .Verifiable();

            _vimData.SetupSet(x => x.LastSearchData = new SearchData(SearchText.NewPattern("foo bar"), SearchKind.ForwardWithWrap, s_options)).Verifiable();
            ProcessWithEnter("foo bar");
            _factory.Verify();

            _vimData.SetupSet(x => x.LastSearchData = new SearchData(SearchText.NewPattern("bar"), SearchKind.ForwardWithWrap, s_options)).Verifiable();
            ProcessWithEnter("bar");
            _factory.Verify();
        }

        [Test]
        public void Status1()
        {
            Create("foo");
            var didRun = false;
            _search.CurrentSearchUpdated += (unused, tuple) =>
                {
                    didRun = true;
                    Assert.IsTrue(tuple.Item2.IsSearchNotFound);
                };
            _search.Begin(SearchKind.ForwardWithWrap);
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Status2()
        {
            Create("foo bar");
            _search.Begin(SearchKind.ForwardWithWrap);
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            var didRun = false;
            _search.CurrentSearchUpdated += (unused, tuple) =>
                {
                    Assert.AreEqual("a", tuple.Item1.Text.RawText);
                    Assert.IsTrue(tuple.Item2.IsSearchNotFound);
                    didRun = true;
                };
            _search.Process(KeyInputUtil.CharToKeyInput('a'));
            Assert.IsTrue(didRun);
        }

        [Test]
        public void Status3()
        {
            Create("foo bar");
            _vimData.SetupSet(x => x.LastSearchData = new SearchData(SearchText.NewPattern("foo"), SearchKind.ForwardWithWrap, s_options)).Verifiable();
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            var didRun = false;
            _search.CurrentSearchCompleted += (unused, tuple) =>
                {
                    Assert.AreEqual("foo", tuple.Item1.Text.RawText);
                    Assert.IsTrue(tuple.Item2.IsSearchNotFound);
                    didRun = true;
                };

            ProcessWithEnter("foo");
            Assert.IsTrue(didRun);
        }

        [Test]
        public void CurrentSearch1()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.Forward);
            _search.Process(KeyInputUtil.CharToKeyInput('B'));
            Assert.AreEqual("B", _search.CurrentSearch.Value.Text.RawText);
        }

        [Test]
        public void CurrentSearch2()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.Forward);
            _search.Process(KeyInputUtil.CharToKeyInput('B'));
            _factory.Verify();
            Assert.AreEqual("B", _search.CurrentSearch.Value.Text.RawText);
        }

        [Test]
        public void CurrentSearch3()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.ForwardWithWrap);
            _search.Process(KeyInputUtil.CharToKeyInput('a'));
            _search.Process(KeyInputUtil.CharToKeyInput('b'));
        }

        [Test]
        public void InSearch1()
        {
            Create("foo bar");
            _search.Begin(SearchKind.Forward);
            Assert.IsTrue(_search.InSearch);
        }

        [Test]
        public void InSearch2()
        {
            Create("foo bar");
            _vimData.SetupSet(x => x.LastSearchData = new SearchData(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.ConsiderSmartCase | SearchOptions.ConsiderIgnoreCase));
            _search.Begin(SearchKind.Forward);
            _search.Process(KeyInputUtil.EnterKey);
            Assert.IsFalse(_search.InSearch);
            Assert.IsFalse(_search.CurrentSearch.IsSome());
        }

        [Test, Description("Cancelling needs to remove the CurrentSearch")]
        public void InSearch3()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None);
            _search.Begin(SearchKind.ForwardWithWrap);
            _search.Process(KeyInputUtil.EscapeKey);
            Assert.IsFalse(_search.InSearch);
        }

        [Test, Description("Backspace with blank search query cancels search")]
        public void Backspace1()
        {
            Create("foo bar");
            _search.Begin(SearchKind.Forward);
            var result = _search.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            Assert.IsTrue(result.IsSearchCancelled);
        }

        [Test, Description("Backspace with text doesn't crash")]
        public void Backspace2()
        {
            Create("foo bar");
            _vimData.SetupSet(x => x.LastSearchData = new SearchData(SearchText.NewPattern(""), SearchKind.Forward, SearchOptions.None));
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), It.IsAny<SnapshotPoint>(), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None)
                .Verifiable();
            _search.Begin(SearchKind.Forward);
            _search.Process(KeyInputUtil.CharToKeyInput('b'));
            var result = _search.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Back));
            Assert.IsTrue(result.IsSearchNeedMore);
            _searchService.Verify();
        }

        [Test]
        public void SearchShouldStartAfterCaretWhenForward()
        {
            Create("foo bar");
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), _textView.GetPoint(1), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None)
                .Verifiable();
            _search.Begin(SearchKind.Forward);
            _search.Process(KeyInputUtil.CharToKeyInput('b'));
            _searchService.Verify();
        }

        [Test]
        public void SearchShouldStartBeforeCaretWhenBackward()
        {
            Create("foo bar");
            _textView.MoveCaretTo(3);
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), _textView.GetPoint(2), _nav.Object))
                .Returns(FSharpOption<SnapshotSpan>.None)
                .Verifiable();
            _search.Begin(SearchKind.Backward);
            _search.Process(KeyInputUtil.CharToKeyInput('b'));
            _searchService.Verify();
        }

        [Test]
        public void SearchForwardThatWrapsShouldUpdateStatus()
        {
            Create("dog cat bear");
            _textView.MoveCaretTo(4);
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), _textView.GetPoint(5), _nav.Object))
                .Returns(FSharpOption.Create(_textView.TextBuffer.GetSpan(0, 1)));
            _vimData.SetupSet(x => x.LastSearchData = It.IsAny<SearchData>());
            _statusUtil.Setup(x => x.OnStatus(Resources.Common_SearchForwardWrapped)).Verifiable();
            _search.DoSearch("d");
            _statusUtil.Verify();
        }

        [Test]
        public void SearchBackwardThatWrapsShouldUpdateStatus()
        {
            Create("dog cat bear");
            _textView.MoveCaretTo(4);
            _searchService
                .Setup(x => x.FindNext(It.IsAny<SearchData>(), _textView.GetPoint(3), _nav.Object))
                .Returns(FSharpOption.Create(_textView.TextBuffer.GetSpan(8, 1)));
            _vimData.SetupSet(x => x.LastSearchData = It.IsAny<SearchData>());
            _statusUtil.Setup(x => x.OnStatus(Resources.Common_SearchBackwardWrapped)).Verifiable();
            _search.DoSearch("b", SearchKind.BackwardWithWrap);
            _statusUtil.Verify();
        }
    }
}