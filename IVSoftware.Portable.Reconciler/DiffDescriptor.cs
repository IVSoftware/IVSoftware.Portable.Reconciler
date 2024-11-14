using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVSoftware.Portable
{
    public class DiffDescriptor
    {
        public DiffDescriptor(IReconcilableDiffs a, IReconcilableDiffs b)
        {
            var builder = new List<IReconcilableDiffs>();
            A = a;
            B = b;
        }
        public IReconcilableDiffs A { get; }
        public IReconcilableDiffs B { get; }
    }
}
