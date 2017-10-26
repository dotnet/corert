// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
    public abstract class Stream : MarshalByRefObject, IDisposable
    {
        public static readonly Stream Null = new NullStream();

        //We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
        // The CopyTo/CopyToAsync buffer is short-lived and is likely to be collected at Gen0, and it offers a significant
        // improvement in Copy performance.
        private const int DefaultCopyBufferSize = 81920;

        // To implement Async IO operations on streams that don't support async IO

        private SemaphoreSlim _asyncActiveSemaphore;

        internal SemaphoreSlim EnsureAsyncActiveSemaphoreInitialized()
        {
            // Lazily-initialize _asyncActiveSemaphore.  As we're never accessing the SemaphoreSlim's
            // WaitHandle, we don't need to worry about Disposing it.
            return LazyInitializer.EnsureInitialized(ref _asyncActiveSemaphore, () => new SemaphoreSlim(1, 1));
        }

        public abstract bool CanRead
        {
            get;
        }

        // If CanSeek is false, Position, Seek, Length, and SetLength should throw.
        public abstract bool CanSeek
        {
            get;
        }

        public virtual bool CanTimeout
        {
            get
            {
                return false;
            }
        }

        public abstract bool CanWrite
        {
            get;
        }

        public abstract long Length
        {
            get;
        }

        public abstract long Position
        {
            get;
            set;
        }

        public virtual int ReadTimeout
        {
            get
            {
                throw new InvalidOperationException(SR.InvalidOperation_TimeoutsNotSupported);
            }
            set
            {
                throw new InvalidOperationException(SR.InvalidOperation_TimeoutsNotSupported);
            }
        }

        public virtual int WriteTimeout
        {
            get
            {
                throw new InvalidOperationException(SR.InvalidOperation_TimeoutsNotSupported);
            }
            set
            {
                throw new InvalidOperationException(SR.InvalidOperation_TimeoutsNotSupported);
            }
        }

        public Task CopyToAsync(Stream destination)
        {
            int bufferSize = GetCopyBufferSize();

            return CopyToAsync(destination, bufferSize);
        }

        public Task CopyToAsync(Stream destination, int bufferSize)
        {
            return CopyToAsync(destination, bufferSize, CancellationToken.None);
        }

        public Task CopyToAsync(Stream destination, CancellationToken cancellationToken)
        {
            int bufferSize = GetCopyBufferSize();

            return CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public virtual Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            StreamHelpers.ValidateCopyToArgs(this, destination, bufferSize);

            return CopyToAsyncInternal(destination, bufferSize, cancellationToken);
        }

        private async Task CopyToAsyncInternal(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            Debug.Assert(destination != null);
            Debug.Assert(bufferSize > 0);
            Debug.Assert(CanRead);
            Debug.Assert(destination.CanWrite);

            byte[] buffer = new byte[bufferSize];
            while (true)
            {
                int bytesRead = await ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0) break;
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            }
        }

        // Reads the bytes from the current stream and writes the bytes to
        // the destination stream until all bytes are read, starting at
        // the current position.
        public void CopyTo(Stream destination)
        {
            int bufferSize = GetCopyBufferSize();

            CopyTo(destination, bufferSize);
        }

        public virtual void CopyTo(Stream destination, int bufferSize)
        {
            StreamHelpers.ValidateCopyToArgs(this, destination, bufferSize);

            byte[] buffer = new byte[bufferSize];
            int read;
            while ((read = Read(buffer, 0, buffer.Length)) != 0)
            {
                destination.Write(buffer, 0, read);
            }
        }

        private int GetCopyBufferSize()
        {
            int bufferSize = DefaultCopyBufferSize;

            if (CanSeek)
            {
                long length = Length;
                long position = Position;
                if (length <= position) // Handles negative overflows
                {
                    // There are no bytes left in the stream to copy.
                    // However, because CopyTo{Async} is virtual, we need to
                    // ensure that any override is still invoked to provide its
                    // own validation, so we use the smallest legal buffer size here.
                    bufferSize = 1;
                }
                else
                {
                    long remaining = length - position;
                    if (remaining > 0)
                    {
                        // In the case of a positive overflow, stick to the default size
                        bufferSize = (int)Math.Min(bufferSize, remaining);
                    }
                }
            }

            return bufferSize;
        }

        public virtual void Close()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            Close();
        }

        protected virtual void Dispose(bool disposing)
        {
            // Note: Never change this to call other virtual methods on Stream
            // like Write, since the state on subclasses has already been 
            // torn down.  This is the last code to run on cleanup for a stream.
        }

        public abstract void Flush();

        public Task FlushAsync()
        {
            return FlushAsync(CancellationToken.None);
        }

        public virtual Task FlushAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            return Task.Factory.StartNew(state => ((Stream)state).Flush(), this,
                cancellationToken, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        [Obsolete("CreateWaitHandle will be removed eventually.  Please use \"new ManualResetEvent(false)\" instead.")]
        protected virtual WaitHandle CreateWaitHandle()
        {
            return new ManualResetEvent(initialState: false);
        }

        public Task<int> ReadAsync(Byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None);
        }

        public virtual Task<int> ReadAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return cancellationToken.IsCancellationRequested ?
                Task.FromCanceled<int>(cancellationToken) :
                Task.Factory.FromAsync(
                    (localBuffer, localOffset, localCount, callback, state) => ((Stream)state).BeginRead(localBuffer, localOffset, localCount, callback, state),
                    iar => ((Stream)iar.AsyncState).EndRead(iar),
                    buffer, offset, count, this);
        }

        public virtual ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (destination.TryGetArray(out ArraySegment<byte> array))
            {
                return new ValueTask<int>(ReadAsync(array.Array, array.Offset, array.Count, cancellationToken));
            }
            else
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(destination.Length);
                return FinishReadAsync(ReadAsync(buffer, 0, destination.Length, cancellationToken), buffer, destination);

                async ValueTask<int> FinishReadAsync(Task<int> readTask, byte[] localBuffer, Memory<byte> localDestination)
                {
                    try
                    {
                        int result = await readTask.ConfigureAwait(false);
                        new Span<byte>(localBuffer, 0, result).CopyTo(localDestination.Span);
                        return result;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(localBuffer);
                    }
                }
            }
        }

        public virtual IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (!CanRead)
            {
                throw new NotSupportedException(SR.NotSupported_UnreadableStream);
            }

            return TaskToApm.Begin(ReadAsyncInternal(buffer, offset, count), callback, state);
        }

        public virtual int EndRead(IAsyncResult asyncResult) =>
            TaskToApm.End<int>(asyncResult);

        private Task<int> ReadAsyncInternal(Byte[] buffer, int offset, int count)
        {
            // To avoid a race with a stream's position pointer & generating race 
            // conditions with internal buffer indexes in our own streams that 
            // don't natively support async IO operations when there are multiple 
            // async requests outstanding, we will serialize the requests.
            return EnsureAsyncActiveSemaphoreInitialized().WaitAsync().ContinueWith((completedWait, s) =>
            {
                Debug.Assert(completedWait.Status == TaskStatus.RanToCompletion);
                var state = (Tuple<Stream, byte[], int, int>)s;
                try
                {
                    return state.Item1.Read(state.Item2, state.Item3, state.Item4); // this.Read(buffer, offset, count);
                }
                finally
                {
                    state.Item1._asyncActiveSemaphore.Release();
                }
            }, Tuple.Create(this, buffer, offset, count), CancellationToken.None, TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        public Task WriteAsync(Byte[] buffer, int offset, int count)
        {
            return WriteAsync(buffer, offset, count, CancellationToken.None);
        }

        public virtual Task WriteAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return cancellationToken.IsCancellationRequested ?
                Task.FromCanceled<int>(cancellationToken) :
                Task.Factory.FromAsync(
                    (localBuffer, localOffset, localCount, callback, state) => ((Stream)state).BeginWrite(localBuffer, localOffset, localCount, callback, state),
                    iar => ((Stream)iar.AsyncState).EndWrite(iar),
                    buffer, offset, count, this);
        }

        public virtual Task WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source.DangerousTryGetArray(out ArraySegment<byte> array))
            {
                return WriteAsync(array.Array, array.Offset, array.Count, cancellationToken);
            }
            else
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(source.Length);
                source.Span.CopyTo(buffer);
                return FinishWriteAsync(WriteAsync(buffer, 0, source.Length, cancellationToken), buffer);

                async Task FinishWriteAsync(Task writeTask, byte[] localBuffer)
                {
                    try
                    {
                        await writeTask.ConfigureAwait(false);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(localBuffer);
                    }
                }
            }
        }

        public virtual IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (!CanWrite)
            {
                throw new NotSupportedException(SR.NotSupported_UnwritableStream);
            }

            return TaskToApm.Begin(WriteAsyncInternal(buffer, offset, count), callback, state);
        }

        public virtual void EndWrite(IAsyncResult asyncResult) =>
            TaskToApm.End(asyncResult);

        private Task WriteAsyncInternal(Byte[] buffer, int offset, int count)
        {
            // To avoid a race with a stream's position pointer & generating race 
            // conditions with internal buffer indexes in our own streams that 
            // don't natively support async IO operations when there are multiple 
            // async requests outstanding, we will serialize the requests.
            return EnsureAsyncActiveSemaphoreInitialized().WaitAsync().ContinueWith((completedWait, s) =>
            {
                Debug.Assert(completedWait.Status == TaskStatus.RanToCompletion);
                var state = (Tuple<Stream, byte[], int, int>)s;
                try
                {
                    state.Item1.Write(state.Item2, state.Item3, state.Item4); // this.Write(buffer, offset, count);
                }
                finally
                {
                    state.Item1._asyncActiveSemaphore.Release();
                }
            }, Tuple.Create(this, buffer, offset, count), CancellationToken.None, TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        public abstract long Seek(long offset, SeekOrigin origin);

        public abstract void SetLength(long value);

        public abstract int Read(byte[] buffer, int offset, int count);

        public virtual int Read(Span<byte> destination)
        {
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;
            byte[] buffer = pool.Rent(destination.Length);
            try
            {
                int numRead = Read(buffer, 0, destination.Length);
                if ((uint)numRead > destination.Length)
                {
                    throw new IOException(SR.IO_StreamTooLong);
                }
                new Span<byte>(buffer, 0, numRead).CopyTo(destination);
                return numRead;
            }
            finally { pool.Return(buffer); }
        }

        // Reads one byte from the stream by calling Read(byte[], int, int). 
        // Will return an unsigned byte cast to an int or -1 on end of stream.
        // This implementation does not perform well because it allocates a new
        // byte[] each time you call it, and should be overridden by any 
        // subclass that maintains an internal buffer.  Then, it can help perf
        // significantly for people who are reading one byte at a time.
        public virtual int ReadByte()
        {
            byte[] oneByteArray = new byte[1];
            int r = Read(oneByteArray, 0, 1);
            if (r == 0)
            {
                return -1;
            }
            return oneByteArray[0];
        }

        public abstract void Write(byte[] buffer, int offset, int count);

        public virtual void Write(ReadOnlySpan<byte> source)
        {
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;
            byte[] buffer = pool.Rent(source.Length);
            try
            {
                source.CopyTo(buffer);
                Write(buffer, 0, source.Length);
            }
            finally { pool.Return(buffer); }
        }

        // Writes one byte from the stream by calling Write(byte[], int, int).
        // This implementation does not perform well because it allocates a new
        // byte[] each time you call it, and should be overridden by any 
        // subclass that maintains an internal buffer.  Then, it can help perf
        // significantly for people who are writing one byte at a time.
        public virtual void WriteByte(byte value)
        {
            byte[] oneByteArray = new byte[1];
            oneByteArray[0] = value;
            Write(oneByteArray, 0, 1);
        }

        public static Stream Synchronized(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (stream is SyncStream)
                return stream;

            return new SyncStream(stream);
        }

        [Obsolete("Do not call or override this method.")]
        protected virtual void ObjectInvariant()
        {
        }

        private sealed class NullStream : Stream
        {
            internal NullStream() { }

            public override bool CanRead => true;

            public override bool CanWrite => true;

            public override bool CanSeek => true;

            public override long Length => 0;

            public override long Position
            {
                get { return 0; }
                set { }
            }

            public override void CopyTo(Stream destination, int bufferSize)
            {
                StreamHelpers.ValidateCopyToArgs(this, destination, bufferSize);

                // After we validate arguments this is a nop.
            }

            public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                // Validate arguments for compat, since previously this
                // method was inherited from Stream, which did check its arguments.
                StreamHelpers.ValidateCopyToArgs(this, destination, bufferSize);

                return cancellationToken.IsCancellationRequested ?
                    Task.FromCanceled(cancellationToken) :
                    Task.CompletedTask;
            }

            protected override void Dispose(bool disposing)
            {
                // Do nothing - we don't want NullStream singleton (static) to be closable
            }

            public override void Flush()
            {
            }

#pragma warning disable 1998 // async method with no await
            public override async Task FlushAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
#pragma warning restore 1998

            public override int Read(byte[] buffer, int offset, int count)
            {
                return 0;
            }

            public override int Read(Span<byte> destination)
            {
                return 0;
            }

#pragma warning disable 1998 // async method with no await
            public override async Task<int> ReadAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return 0;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return 0;
            }
#pragma warning restore 1998

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
                TaskToApm.Begin(ReadAsync(buffer, offset, count, CancellationToken.None), callback, state);

            public override int EndRead(IAsyncResult asyncResult) =>
                TaskToApm.End<int>(asyncResult);

            public override int ReadByte()
            {
                return -1;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
            }

            public override void Write(ReadOnlySpan<byte> source)
            {
            }

#pragma warning disable 1998 // async method with no await
            public override async Task WriteAsync(Byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            public override async Task WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
#pragma warning restore 1998

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
                TaskToApm.Begin(WriteAsync(buffer, offset, count, CancellationToken.None), callback, state);

            public override void EndWrite(IAsyncResult asyncResult) =>
                TaskToApm.End(asyncResult);

            public override void WriteByte(byte value)
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return 0;
            }

            public override void SetLength(long length)
            {
            }
        }

        // SyncStream is a wrapper around a stream that takes 
        // a lock for every operation making it thread safe.
        private sealed class SyncStream : Stream, IDisposable
        {
            private Stream _stream;

            internal SyncStream(Stream stream)
            {
                if (stream == null)
                    throw new ArgumentNullException(nameof(stream));

                _stream = stream;
            }

            public override bool CanRead => _stream.CanRead;

            public override bool CanWrite => _stream.CanWrite;

            public override bool CanSeek => _stream.CanSeek;

            public override bool CanTimeout => _stream.CanTimeout;

            public override long Length
            {
                get
                {
                    lock (_stream)
                    {
                        return _stream.Length;
                    }
                }
            }

            public override long Position
            {
                get
                {
                    lock (_stream)
                    {
                        return _stream.Position;
                    }
                }
                set
                {
                    lock (_stream)
                    {
                        _stream.Position = value;
                    }
                }
            }

            public override int ReadTimeout
            {
                get
                {
                    return _stream.ReadTimeout;
                }
                set
                {
                    _stream.ReadTimeout = value;
                }
            }

            public override int WriteTimeout
            {
                get
                {
                    return _stream.WriteTimeout;
                }
                set
                {
                    _stream.WriteTimeout = value;
                }
            }

            // In the off chance that some wrapped stream has different 
            // semantics for Close vs. Dispose, let's preserve that.
            public override void Close()
            {
                lock (_stream)
                {
                    try
                    {
                        _stream.Close();
                    }
                    finally
                    {
                        base.Dispose(true);
                    }
                }
            }

            protected override void Dispose(bool disposing)
            {
                lock (_stream)
                {
                    try
                    {
                        // Explicitly pick up a potentially methodimpl'ed Dispose
                        if (disposing)
                            ((IDisposable)_stream).Dispose();
                    }
                    finally
                    {
                        base.Dispose(disposing);
                    }
                }
            }

            public override void Flush()
            {
                lock (_stream)
                    _stream.Flush();
            }

            public override int Read(byte[] bytes, int offset, int count)
            {
                lock (_stream)
                    return _stream.Read(bytes, offset, count);
            }

            public override int Read(Span<byte> destination)
            {
                lock (_stream)
                    return _stream.Read(destination);
            }

            public override int ReadByte()
            {
                lock (_stream)
                    return _stream.ReadByte();
            }

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
            {
                throw new NotImplementedException(); // TODO: https://github.com/dotnet/corert/issues/3251
                //bool overridesBeginRead = _stream.HasOverriddenBeginEndRead();

                //lock (_stream)
                //{
                //    // If the Stream does have its own BeginRead implementation, then we must use that override.
                //    // If it doesn't, then we'll use the base implementation, but we'll make sure that the logic
                //    // which ensures only one asynchronous operation does so with an asynchronous wait rather
                //    // than a synchronous wait.  A synchronous wait will result in a deadlock condition, because
                //    // the EndXx method for the outstanding async operation won't be able to acquire the lock on
                //    // _stream due to this call blocked while holding the lock.
                //    return overridesBeginRead ?
                //        _stream.BeginRead(buffer, offset, count, callback, state) :
                //        _stream.BeginReadInternal(buffer, offset, count, callback, state, serializeAsynchronously: true, apm: true);
                //}
            }

            public override int EndRead(IAsyncResult asyncResult)
            {
                if (asyncResult == null)
                    throw new ArgumentNullException(nameof(asyncResult));

                lock (_stream)
                    return _stream.EndRead(asyncResult);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                lock (_stream)
                    return _stream.Seek(offset, origin);
            }

            public override void SetLength(long length)
            {
                lock (_stream)
                    _stream.SetLength(length);
            }

            public override void Write(byte[] bytes, int offset, int count)
            {
                lock (_stream)
                    _stream.Write(bytes, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> source)
            {
                lock (_stream)
                    _stream.Write(source);
            }

            public override void WriteByte(byte b)
            {
                lock (_stream)
                    _stream.WriteByte(b);
            }

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
            {
                throw new NotImplementedException(); // TODO: https://github.com/dotnet/corert/issues/3251
                //bool overridesBeginWrite = _stream.HasOverriddenBeginEndWrite();

                //lock (_stream)
                //{
                //    // If the Stream does have its own BeginWrite implementation, then we must use that override.
                //    // If it doesn't, then we'll use the base implementation, but we'll make sure that the logic
                //    // which ensures only one asynchronous operation does so with an asynchronous wait rather
                //    // than a synchronous wait.  A synchronous wait will result in a deadlock condition, because
                //    // the EndXx method for the outstanding async operation won't be able to acquire the lock on
                //    // _stream due to this call blocked while holding the lock.
                //    return overridesBeginWrite ?
                //        _stream.BeginWrite(buffer, offset, count, callback, state) :
                //        _stream.BeginWriteInternal(buffer, offset, count, callback, state, serializeAsynchronously: true, apm: true);
                //}
            }

            public override void EndWrite(IAsyncResult asyncResult)
            {
                if (asyncResult == null)
                    throw new ArgumentNullException(nameof(asyncResult));

                lock (_stream)
                    _stream.EndWrite(asyncResult);
            }
        }
    }
}
