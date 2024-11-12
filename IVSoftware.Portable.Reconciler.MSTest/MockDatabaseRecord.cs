using IVSoftware.Portable.Disposable;
using IVSoftware.WinOS.MSTest.Extensions;
using Newtonsoft.Json;
using SQLite;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static IVSoftware.Portable.Reconciler;
using IgnoreAttribute = SQLite.IgnoreAttribute;

namespace IVSoftware.Portable.Static.Reconciler.MSTest
{
    #region S T A B L E
    public class MockDatabaseRecord : IReconcilable<MockDatabaseRecord>, INotifyPropertyChanged
    {
        #region I N T E R F A C E
        IComparable IReconcilable.UIDCompareProperty => Uid;

        IComparable IReconcilable.VersionCompareProperty => TimeStamp;

        IComparable? IReconcilable.ResultSorterProperty => null;
        MockDatabaseRecord IReconcilable<MockDatabaseRecord>.UpdateFrom(MockDatabaseRecord other)
        {
            if (other.IsModified)
            {
                Debug.WriteLine($"ADVISORY: Collision! {Uid}");
            }
            TimeStamp = other.TimeStamp;

            // Canonically, both items are considered non-modified now.
            other.IsModified = IsModified = false;

            // This is to provide inline access to review
            // the changes for validation or unit test.
            return this;
        }
        #endregion I N T E R F A C E


        [PrimaryKey]
        public string Uid { get; init; } = Guid.NewGuid().ToString().ToUpper();
        public DateTime TimeStamp { get; set; }

        [Unique]
        public string? Description { get; set; }
        public string JsonProperties
        {
            get => JsonConvert.SerializeObject(Properties, Formatting.Indented);
            set
            {
                using (DHostLoading.GetToken())
                    Properties =
                        JsonConvert
                        .DeserializeObject<Dictionary<string, object>>(value) ??
                        new Dictionary<string, object>();
            }
        }
        [Ignore]
        public Dictionary<string, object> Properties
        {
            get
            {
                if (_properties is null)
                {
                    _properties = new Dictionary<string, object>();
                }
                return _properties;
            }
            set => _properties = value;
        }
        Dictionary<string, object>? _properties = default;

        static DisposableHost DHostLoading { get; } = new DisposableHost();
        public bool IsModified { get; set; }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (DHostLoading.IsZero())
            {
                // Proposed: Modify record timestamp.
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString()
        {
            return $@"UID: {Uid}, TimeStamp: {TimeStamp:MM/dd/yyyy HH\:mm\:ss}, Description: {Description} modified={IsModified}";
        }

        public object Clone() => this.Clone<MockDatabaseRecord>();

    }

    public class MockReconcilableDatabaseRecord : MockDatabaseRecord, IReconcilable
    {
        public IComparable UIDCompareProperty => Uid;

        public IComparable VersionCompareProperty => TimeStamp;

        public IComparable? ResultSorterProperty => null;
    }
    #endregion S T A B L E
}
