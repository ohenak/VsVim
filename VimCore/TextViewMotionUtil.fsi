﻿#light

namespace Vim

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations

type internal TextViewMotionUtil =
    new : ITextView * IMarkMap * IVimLocalSettings -> TextViewMotionUtil

    interface ITextViewMotionUtil

