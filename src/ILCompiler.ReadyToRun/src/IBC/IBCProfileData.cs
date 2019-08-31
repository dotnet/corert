using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Internal.TypeSystem;
using System.Text;
using ILCompiler;

namespace ILCompiler.IBC
{

    public class IBCProfileData : ProfileData
    {
        public IBCProfileData(bool partialNGen, List<MethodProfileData> methodData)
        {
            foreach (var data in methodData)
            {
                if (_methodData.ContainsKey(data.Method))
                    throw new Exception("Multiple copies of data for the same method"); // TODO, I think this is actually valid, but lets see

                _methodData.Add(data.Method, data);
            }
            _partialNGen = partialNGen;
            _methodDataList = ImmutableArray.CreateRange(methodData);
        }

        private Dictionary<MethodDesc, MethodProfileData> _methodData = new Dictionary<MethodDesc, MethodProfileData>();
        private ImmutableArray<MethodProfileData> _methodDataList;
        private bool _partialNGen;

        public override bool PartialNGen { get { return _partialNGen; } }

        public override MethodProfileData GetMethodProfileData(MethodDesc m)
        {
            MethodProfileData profileData;
            _methodData.TryGetValue(m, out profileData);
            return profileData;
        }

        public override IReadOnlyList<MethodProfileData> GetAllMethodProfileData()
        {
            return _methodDataList;
        }

        public override byte[] GetMethodBlockCount(MethodDesc m)
        {
            throw new NotImplementedException();
        }
    }
}
