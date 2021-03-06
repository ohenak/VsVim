﻿#light

namespace Vim
open System.Text.RegularExpressions

/// Options which can be passed to a vim regex.  These override anything 
/// which is found embedded in the regex.  For example IgnoreCase will 
/// override an embedded \C in the pattern or a noignorecase option
[<System.Flags>]
type VimRegexOptions = 
    | None = 0
    | Compiled = 0x1

    /// Causes the regex to ignore case.  This will override any embedded \C 
    /// modifier in the pattern or a noignore case option 
    | IgnoreCase = 0x2

    /// Causes the regex to consider case.  This will override any embedded \c 
    /// modifier in the pattern or a noignore case option 
    | OrdinalCase = 0x4

    /// Causes the regex to begin in magic mode.  This can be disabled later in
    /// the regex with a \M specifier
    | Magic = 0x8

    /// Causes the regex to begin in nomagic mode.  This can be disabled later in
    /// the regex with a \m specifier
    | NoMagic = 0x10

/// Represents a Vim style regular expression 
[<Sealed>]
type VimRegex =

    /// Vim Pattern of the Regular expression
    member VimPattern : string

    /// Pattern of the BCL version of the regular expression
    member RegexPattern : string

    /// The underlying BCL Regex expression.  
    member Regex : Regex

    /// Does the string match the text
    member IsMatch : pattern:string -> bool

    /// Matches the regex against the specified input and does the replacement 
    /// as specified "count" times
    member Replace : input:string -> replacement:string -> count:int -> string 

    /// Replace a single occurance inside the given string
    member ReplaceOne : input:string -> replacement:string -> string 

    /// Matches the regex against the specified input and does the replacement 
    /// as specified.  If there is currently no regex then None will be returned
    member ReplaceAll : input:string -> replacement:string -> string 

[<Sealed>]
type VimRegexFactory = 

    new : IVimGlobalSettings -> VimRegexFactory

    member Create : pattern:string -> VimRegex option

    member CreateForSearchText : text:SearchText -> VimRegex option

    member CreateForSubstituteFlags : pattern:string -> SubstituteFlags -> VimRegex option

    member CreateWithOptions : pattern:string -> options:VimRegexOptions -> VimRegex option



