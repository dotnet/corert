// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Internal.Metadata.NativeFormat.Writer;

namespace ILCompiler.Metadata
{
    internal struct EntityMap<TEntity, TRecord>
    {
        private Dictionary<TEntity, TRecord> _map;

        public EntityMap(IEqualityComparer<TEntity> comparer)
        {
            _map = new Dictionary<TEntity, TRecord>(comparer);
        }

        public TRecord GetOrCreate<TConcreteEntity, TConcreteRecord>(TConcreteEntity entity, Action<TConcreteEntity, TConcreteRecord> initializer)
            where TConcreteEntity : TEntity
            where TConcreteRecord : TRecord, new()
        {
            TRecord record;
            if (!_map.TryGetValue(entity, out record))
            {
                // We are externalizing the allocation instead of having a 'creator' delegate
                // because initializer might end up recursing into GetOrCreate for the same entity.
                // EntityMap needs to be ready to return a pointer to the currently initialized record.

                // The transform doesn't care that the record is not fully initialized yet
                // since we're not reading it at this stage.

                // Example:
                //
                // class FooAttribute : Attribute
                // {
                //     [FooAttribute]
                //     public FooAttribute()
                //     {
                //     }
                // }
                //
                // In here, while we're emitting the record for FooAttribute..ctor, we need
                // a pointer to the record for FooAttribute..ctor because that's what the
                // constructor of the custom attribute applied to the constructor.

                TConcreteRecord concreteRecord = new TConcreteRecord();
                _map.Add(entity, concreteRecord);

                initializer(entity, concreteRecord);

                return concreteRecord;
            }

            return record;
        }
    }    
}
