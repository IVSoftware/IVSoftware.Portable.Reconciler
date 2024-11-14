using System;
using System.Collections.Generic;
using System.Text;

namespace IVSoftware.Portable
{
    public enum CompareUIDResult
    {
        /// <summary>
        /// A occurs sooner in list than B
        /// </summary>
        OnlyInA = -1,
        /// <summary>
        /// The UID is the same.
        /// </summary>
        InBoth = 0,
        /// <summary>
        /// B occurs sooner in list than A
        /// </summary>
        OnlyInB = 1,
    }

    /// <summary>
    /// POLARITY REVERSED: Whether the property is DateTime or a 
    /// version like 1.0.00, the greater-than result is the newest. 
    /// </summary>
    public enum CompareVersionResult
    {
        /// <summary>
        /// A is Higher, therefore newer
        /// </summary>
        NewerIsX = 1,

        /// <summary>
        /// The versions are identical
        /// </summary>
        Equal = 0,

        /// <summary>
        /// B is higher, therefore newer.
        /// </summary>
        NewerIsY = -1,
    }

    public enum OnReconcile
    {
        /// <summary>
        /// Ensure that both collections contain all items by appending missing items as necessary.
        /// </summary>
        Append,

        /// <summary>
        /// Ensure that both collections contain the same items by trimming excess items.
        /// </summary>
        Trim,

        /// <summary>
        /// Adjust collection B to match collection A, either by trimming excess items or appending missing items.
        /// </summary>
        TakeA,

        /// <summary>
        /// Adjust collection A to match collection B, either by trimming excess items or appending missing items.
        /// </summary>
        TakeB,
    }


    /// <summary>
    /// Defines the available methods for handling collisions between records in listA and listB. A collision 
    /// occurs when a record UID exists in both lists and has been marked as modified (dirty) in both. Version 2
    /// of the package endeavors to correct the implied notion that in a pair where one record is e.g. 'NewerInA',
    /// the newer record is preeminent and can be accepted without question. In other words, it's 'not wrong' that
    /// one of the items is newer; it's just that reporting it as such may not tell the whole story.
    /// </summary>

    public enum OnReport
    {
        /// <summary>
        /// - Version 2 collision handling capability is disabled.
        /// - The original Version 1 format will be returned for ToString().
        /// - This is the default.
        /// </summary>
        Disabled,

        /// <summary>
        /// - Version 2 collision handling capability is enabled.
        /// - The updated Version 2 collision reporting format will be returned for ToString().
        /// - Deterministic collections Equal, NewerInA, and NewerInB will return results consistent
        ///   with the Version 1 contract.
        /// </summary>
        /// <remarks>
        /// - A collision happens when a record with the same UID appears in both lists and is flagged as modified 
        ///   in both. In Version 2, there's a shift in understanding: while one record in a pair may be "NewerInA,"  
        ///   this doesn’t imply that the newer record is unquestionably correct or final — it simply indicates
        ///   that one is newer than the other. But this alone might not capture the full context.
        /// 
        /// - There are also substantial differences between V1 and V2 formats returned by the respective 
        ///   ToString() method, and this may be critical, for example, if there is Unit Testing that 
        ///   relies on this as a test metric.
        /// </remarks>
        Report,
    }


    /// <summary>
    /// Defines the available methods for handling collisions between records in listA and listB. A collision 
    /// occurs when a record UID exists in both lists and has been marked as modified (dirty) in both. Version 2
    /// of the package endeavors to correct the implied notion that in a pair where one record is e.g. 'NewerInA',
    /// the newer record is preeminent and can be accepted without question. In other words, it's 'not wrong' that
    /// one of the items is newer; it's just that reporting it as such may not tell the whole story.
    /// </summary>

    public enum OnCollision
    {
        /// <summary>
        /// - Version 2 collision handling capability is disabled.
        /// - The original Version 1 format will be returned for ToString().
        /// - This is the default.
        /// </summary>
        Disabled,

        /// <summary>
        /// - Version 2 collision handling capability is enabled.
        /// - The updated Version 2 collision reporting format will be returned for ToString().
        /// - Deterministic collections Equal, NewerInA, and NewerInB will return results consistent
        ///   with the Version 1 contract.
        /// </summary>
        /// <remarks>
        /// - A collision happens when a record with the same UID appears in both lists and is flagged as modified 
        ///   in both. In Version 2, there's a shift in understanding: while one record in a pair may be "NewerInA,"  
        ///   this doesn’t imply that the newer record is unquestionably correct or final — it simply indicates
        ///   that one is newer than the other. But this alone might not capture the full context.
        /// 
        /// - There are also substantial differences between V1 and V2 formats returned by the respective 
        ///   ToString() method, and this may be critical, for example, if there is Unit Testing that 
        ///   relies on this as a test metric.
        /// </remarks>
        Report,

        /// <summary>
        /// Alternative collision handling mode introduced in Version 2 (V2).
        /// Detects when a record has been modified in both listA and listB and places such records in the
        /// "ModifiedInBoth" category instead of any determinate collection.
        /// </summary>
        /// <remarks>
        /// The Move option, introduced in Version 2, adds clarity by detecting records modified in both lists and
        /// categorizing them as "ModifiedInBoth." This prevents them from being placed in collections like NewerInA
        /// or NewerInB, which expect determinate entries. This approach avoids the ambiguity of records with conflicting
        /// modifications being reported in a manner that implies the result is determinate.
        /// </remarks>
        Move,
    }
}
