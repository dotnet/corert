// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// AsyncMethodBuilder.cs
//

//
// Compiler-targeted types that build tasks for use as the return types of asynchronous methods.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

using Internal.Runtime.Augments;

using AsyncStatus = Internal.Runtime.Augments.AsyncStatus;
using CausalityRelation = Internal.Runtime.Augments.CausalityRelation;
using CausalitySource = Internal.Runtime.Augments.CausalitySource;
using CausalityTraceLevel = Internal.Runtime.Augments.CausalityTraceLevel;
using CausalitySynchronousWork = Internal.Runtime.Augments.CausalitySynchronousWork;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Provides a builder for asynchronous methods that return void.
    /// This type is intended for compiler use only.
    /// </summary>
    public struct AsyncVoidMethodBuilder
    {
        /// <summary>Action to invoke the state machine's MoveNext.</summary>
        private Action m_moveNextAction;
        /// <summary>The synchronization context associated with this operation.</summary>
        private SynchronizationContext m_synchronizationContext;

        //WARNING: We allow diagnostic tools to directly inspect this member (m_task). 
        //See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        //Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        //Get in touch with the diagnostics team if you have questions.
        /// <summary>Task used for debugging and logging purposes only.  Lazily initialized.</summary>
        private Task m_task;

        /// <summary>Initializes a new <see cref="AsyncVoidMethodBuilder"/>.</summary>
        /// <returns>The initialized <see cref="AsyncVoidMethodBuilder"/>.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static AsyncVoidMethodBuilder Create()
        {
            SynchronizationContext sc = SynchronizationContext.Current;
            if (sc != null)
                sc.OperationStarted();
            // On ProjectN we will eagerly initalize the task and it's Id if the debugger is attached
            AsyncVoidMethodBuilder avmb = new AsyncVoidMethodBuilder() { m_synchronizationContext = sc };
            avmb.m_task = avmb.GetTaskIfDebuggingEnabled();
            if (avmb.m_task != null)
            {
                int i = avmb.m_task.Id;
            }
            return avmb;
        }

        /// <summary>Initiates the builder's execution with the associated state machine.</summary>
        /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
        /// <param name="stateMachine">The state machine instance, passed by reference.</param>
        /// <exception cref="System.ArgumentNullException">The <paramref name="stateMachine"/> argument was null (Nothing in Visual Basic).</exception>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            AsyncMethodBuilderCore.Start(ref stateMachine);
        }

        /// <summary>Associates the builder with the state machine it represents.</summary>
        /// <param name="stateMachine">The heap-allocated state machine object.</param>
        /// <exception cref="System.ArgumentNullException">The <paramref name="stateMachine"/> argument was null (Nothing in Visual Basic).</exception>
        /// <exception cref="System.InvalidOperationException">The builder is incorrectly initialized.</exception>
        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            AsyncMethodBuilderCore.SetStateMachine(stateMachine, m_moveNextAction); // argument validation handled by AsyncMethodBuilderCore
        }

        /// <summary>
        /// Schedules the specified state machine to be pushed forward when the specified awaiter completes.
        /// </summary>
        /// <typeparam name="TAwaiter">Specifies the type of the awaiter.</typeparam>
        /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
        /// <param name="awaiter">The awaiter.</param>
        /// <param name="stateMachine">The state machine.</param>
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            AsyncMethodBuilderCore.CallOnCompleted(
                AsyncMethodBuilderCore.GetCompletionAction(ref m_moveNextAction, ref stateMachine, this.GetTaskIfDebuggingEnabled()),
                ref awaiter);
        }

        /// <summary>
        /// Schedules the specified state machine to be pushed forward when the specified awaiter completes.
        /// </summary>
        /// <typeparam name="TAwaiter">Specifies the type of the awaiter.</typeparam>
        /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
        /// <param name="awaiter">The awaiter.</param>
        /// <param name="stateMachine">The state machine.</param>
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            AsyncMethodBuilderCore.CallUnsafeOnCompleted(
                AsyncMethodBuilderCore.GetCompletionAction(ref m_moveNextAction, ref stateMachine, this.GetTaskIfDebuggingEnabled()),
                ref awaiter);
        }

        /// <summary>Completes the method builder successfully.</summary>
        public void SetResult()
        {
            Task taskIfDebuggingEnabled = this.GetTaskIfDebuggingEnabled();
            if (taskIfDebuggingEnabled != null)
            {
                if (DebuggerSupport.LoggingOn)
                    DebuggerSupport.TraceOperationCompletion(CausalityTraceLevel.Required, taskIfDebuggingEnabled, AsyncStatus.Completed);
                DebuggerSupport.RemoveFromActiveTasks(taskIfDebuggingEnabled);
            }

            NotifySynchronizationContextOfCompletion();
        }

        /// <summary>Faults the method builder with an exception.</summary>
        /// <param name="exception">The exception that is the cause of this fault.</param>
        /// <exception cref="System.ArgumentNullException">The <paramref name="exception"/> argument is null (Nothing in Visual Basic).</exception>
        /// <exception cref="System.InvalidOperationException">The builder is not initialized.</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetException(Exception exception)
        {
            Task taskIfDebuggingEnabled = this.GetTaskIfDebuggingEnabled();
            if (taskIfDebuggingEnabled != null)
            {
                if (DebuggerSupport.LoggingOn)
                    DebuggerSupport.TraceOperationCompletion(CausalityTraceLevel.Required, taskIfDebuggingEnabled, AsyncStatus.Error);
            }

            AsyncMethodBuilderCore.ThrowAsync(exception, m_synchronizationContext);
            NotifySynchronizationContextOfCompletion();
        }

        /// <summary>Notifies the current synchronization context that the operation completed.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void NotifySynchronizationContextOfCompletion()
        {
            if (m_synchronizationContext != null)
            {
                try
                {
                    m_synchronizationContext.OperationCompleted();
                }
                catch (Exception exc)
                {
                    // If the interaction with the SynchronizationContext goes awry,
                    // fall back to propagating on the ThreadPool.
                    AsyncMethodBuilderCore.ThrowAsync(exc, targetContext: null);
                }
            }
        }

        // This property lazily instantiates the Task in a non-thread-safe manner.  
        internal Task Task
        {
            get
            {
                if (m_task == null) m_task = new Task();
                return m_task;
            }
        }


        /// <summary>
        /// Gets an object that may be used to uniquely identify this builder to the debugger.
        /// </summary>
        /// <remarks>
        /// This property lazily instantiates the ID in a non-thread-safe manner.  
        /// It must only be used by the debugger and only in a single-threaded manner.
        /// </remarks>
        private object ObjectIdForDebugger
        {
            get
            {
                return this.Task;
            }
        }
    }

    /// <summary>
    /// Provides a builder for asynchronous methods that return <see cref="System.Threading.Tasks.Task"/>.
    /// This type is intended for compiler use only.
    /// </summary>
    /// <remarks>
    /// AsyncTaskMethodBuilder is a value type, and thus it is copied by value.
    /// Prior to being copied, one of its Task, SetResult, or SetException members must be accessed,
    /// or else the copies may end up building distinct Task instances.
    /// </remarks>
    public struct AsyncTaskMethodBuilder
    {
        /// <summary>A cached VoidTaskResult task used for builders that complete synchronously.</summary>
        private readonly static Task<VoidTaskResult> s_cachedCompleted = AsyncTaskCache.CreateCacheableTask<VoidTaskResult>(default(VoidTaskResult));

        private Action m_moveNextAction;

        // WARNING: We allow diagnostic tools to directly inspect this member (m_task). 
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.
        private Task<VoidTaskResult> m_task;

        /// <summary>Initializes a new <see cref="AsyncTaskMethodBuilder"/>.</summary>
        /// <returns>The initialized <see cref="AsyncTaskMethodBuilder"/>.</returns>
        public static AsyncTaskMethodBuilder Create()
        {
            AsyncTaskMethodBuilder atmb = default(AsyncTaskMethodBuilder);
            // On ProjectN we will eagerly initalize the task and it's Id if the debugger is attached
            atmb.m_task = (Task<VoidTaskResult>)atmb.GetTaskIfDebuggingEnabled();
            if (atmb.m_task != null)
            {
                int i = atmb.m_task.Id;
            }
            return atmb;
        }

        /// <summary>Initiates the builder's execution with the associated state machine.</summary>
        /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
        /// <param name="stateMachine">The state machine instance, passed by reference.</param>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            AsyncMethodBuilderCore.Start(ref stateMachine);
        }

        /// <summary>Associates the builder with the state machine it represents.</summary>
        /// <param name="stateMachine">The heap-allocated state machine object.</param>
        /// <exception cref="System.ArgumentNullException">The <paramref name="stateMachine"/> argument was null (Nothing in Visual Basic).</exception>
        /// <exception cref="System.InvalidOperationException">The builder is incorrectly initialized.</exception>
        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            AsyncMethodBuilderCore.SetStateMachine(stateMachine, m_moveNextAction); // argument validation handled by AsyncMethodBuilderCore
        }

        /// <summary>
        /// Schedules the specified state machine to be pushed forward when the specified awaiter completes.
        /// </summary>
        /// <typeparam name="TAwaiter">Specifies the type of the awaiter.</typeparam>
        /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
        /// <param name="awaiter">The awaiter.</param>
        /// <param name="stateMachine">The state machine.</param>
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            EnsureTaskCreated();
            AsyncMethodBuilderCore.CallOnCompleted(
                AsyncMethodBuilderCore.GetCompletionAction(ref m_moveNextAction, ref stateMachine, this.GetTaskIfDebuggingEnabled()),
                ref awaiter);
        }

        /// <summary>
        /// Schedules the specified state machine to be pushed forward when the specified awaiter completes.
        /// </summary>
        /// <typeparam name="TAwaiter">Specifies the type of the awaiter.</typeparam>
        /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
        /// <param name="awaiter">The awaiter.</param>
        /// <param name="stateMachine">The state machine.</param>
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            EnsureTaskCreated();
            AsyncMethodBuilderCore.CallUnsafeOnCompleted(
                AsyncMethodBuilderCore.GetCompletionAction(ref m_moveNextAction, ref stateMachine, this.GetTaskIfDebuggingEnabled()),
                ref awaiter);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureTaskCreated()
        {
            if (m_task == null)
                m_task = new Task<VoidTaskResult>();
        }

        /// <summary>Gets the <see cref="System.Threading.Tasks.Task"/> for this builder.</summary>
        /// <returns>The <see cref="System.Threading.Tasks.Task"/> representing the builder's asynchronous operation.</returns>
        /// <exception cref="System.InvalidOperationException">The builder is not initialized.</exception>
        public Task Task
        {
            get
            {
                return m_task ?? (m_task = new Task<VoidTaskResult>());
            }
        }

        /// <summary>
        /// Completes the <see cref="System.Threading.Tasks.Task"/> in the 
        /// <see cref="System.Threading.Tasks.TaskStatus">RanToCompletion</see> state.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">The builder is not initialized.</exception>
        /// <exception cref="System.InvalidOperationException">The task has already completed.</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetResult()
        {
            var task = m_task;
            if (task == null)
                m_task = s_cachedCompleted;
            else
            {
                if (DebuggerSupport.LoggingOn)
                    DebuggerSupport.TraceOperationCompletion(CausalityTraceLevel.Required, task, AsyncStatus.Completed);
                DebuggerSupport.RemoveFromActiveTasks(task);

                if (!task.TrySetResult(default(VoidTaskResult)))
                    throw new InvalidOperationException(SR.TaskT_TransitionToFinal_AlreadyCompleted);
            }
        }

        /// <summary>
        /// Completes the <see cref="System.Threading.Tasks.Task"/> in the 
        /// <see cref="System.Threading.Tasks.TaskStatus">Faulted</see> state with the specified exception.
        /// </summary>
        /// <param name="exception">The <see cref="System.Exception"/> to use to fault the task.</param>
        /// <exception cref="System.ArgumentNullException">The <paramref name="exception"/> argument is null (Nothing in Visual Basic).</exception>
        /// <exception cref="System.InvalidOperationException">The builder is not initialized.</exception>
        /// <exception cref="System.InvalidOperationException">The task has already completed.</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetException(Exception exception) { SetException(this.Task, exception); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void SetException(Task task, Exception exception)
        {
            if (exception == null) throw new ArgumentNullException("exception");
            Contract.EndContractBlock();

            // If the exception represents cancellation, cancel the task.  Otherwise, fault the task.
            var oce = exception as OperationCanceledException;
            bool successfullySet = oce != null ?
                task.TrySetCanceled(oce.CancellationToken, oce) :
                task.TrySetException(exception);

            // Unlike with TaskCompletionSource, we do not need to spin here until m_task is completed,
            // since AsyncTaskMethodBuilder.SetException should not be immediately followed by any code
            // that depends on the task having completely completed.  Moreover, with correct usage, 
            // SetResult or SetException should only be called once, so the Try* methods should always
            // return true, so no spinning would be necessary anyway (the spinning in TCS is only relevant
            // if another thread won the race to complete the task).

            if (!successfullySet)
            {
                throw new InvalidOperationException(SR.TaskT_TransitionToFinal_AlreadyCompleted);
            }
        }

        /// <summary>
        /// Called by the debugger to request notification when the first wait operation
        /// (await, Wait, Result, etc.) on this builder's task completes.
        /// </summary>
        /// <param name="enabled">
        /// true to enable notification; false to disable a previously set notification.
        /// </param>
        internal void SetNotificationForWaitCompletion(bool enabled)
        {
            // Get the task (forcing initialization if not already initialized), and set debug notification
            Task.SetNotificationForWaitCompletion(enabled);
        }

        /// <summary>
        /// Gets an object that may be used to uniquely identify this builder to the debugger.
        /// </summary>
        /// <remarks>
        /// This property lazily instantiates the ID in a non-thread-safe manner.  
        /// It must only be used by the debugger, and only in a single-threaded manner
        /// when no other threads are in the middle of accessing this property or this.Task.
        /// </remarks>
        private object ObjectIdForDebugger { get { return this.Task; } }
    }

    /// <summary>
    /// Provides a builder for asynchronous methods that return <see cref="System.Threading.Tasks.Task{TResult}"/>.
    /// This type is intended for compiler use only.
    /// </summary>
    /// <remarks>
    /// AsyncTaskMethodBuilder{TResult} is a value type, and thus it is copied by value.
    /// Prior to being copied, one of its Task, SetResult, or SetException members must be accessed,
    /// or else the copies may end up building distinct Task instances.
    /// </remarks>
    public struct AsyncTaskMethodBuilder<TResult>
    {
#if false
        /// <summary>A cached task for default(TResult).</summary>
        internal readonly static Task<TResult> s_defaultResultTask = AsyncTaskCache.CreateCacheableTask(default(TResult));
#endif

        private Action m_moveNextAction;

        // WARNING: We allow diagnostic tools to directly inspect this member (m_task).
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.
        // WARNING: For performance reasons, the m_task field is lazily initialized.
        //          For correct results, the struct AsyncTaskMethodBuilder<TResult> must 
        //          always be used from the same location/copy, at least until m_task is 
        //          initialized.  If that guarantee is broken, the field could end up being 
        //          initialized on the wrong copy.
        /// <summary>The lazily-initialized built task.</summary>
        private Task<TResult> m_task; // lazily-initialized: must not be readonly

        /// <summary>Initializes a new <see cref="AsyncTaskMethodBuilder"/>.</summary>
        /// <returns>The initialized <see cref="AsyncTaskMethodBuilder"/>.</returns>
        public static AsyncTaskMethodBuilder<TResult> Create()
        {
            AsyncTaskMethodBuilder<TResult> atmb = new AsyncTaskMethodBuilder<TResult>();
            // On ProjectN we will eagerly initalize the task and it's Id if the debugger is attached
            atmb.m_task = (Task<TResult>)atmb.GetTaskIfDebuggingEnabled();
            if (atmb.m_task != null)
            {
                int i = atmb.m_task.Id;
            }
            return atmb;
        }

        /// <summary>Initiates the builder's execution with the associated state machine.</summary>
        /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
        /// <param name="stateMachine">The state machine instance, passed by reference.</param>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            AsyncMethodBuilderCore.Start(ref stateMachine); // argument validation handled by AsyncMethodBuilderCore
        }

        /// <summary>Associates the builder with the state machine it represents.</summary>
        /// <param name="stateMachine">The heap-allocated state machine object.</param>
        /// <exception cref="System.ArgumentNullException">The <paramref name="stateMachine"/> argument was null (Nothing in Visual Basic).</exception>
        /// <exception cref="System.InvalidOperationException">The builder is incorrectly initialized.</exception>
        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            AsyncMethodBuilderCore.SetStateMachine(stateMachine, m_moveNextAction); // argument validation handled by AsyncMethodBuilderCore
        }

        /// <summary>
        /// Schedules the specified state machine to be pushed forward when the specified awaiter completes.
        /// </summary>
        /// <typeparam name="TAwaiter">Specifies the type of the awaiter.</typeparam>
        /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
        /// <param name="awaiter">The awaiter.</param>
        /// <param name="stateMachine">The state machine.</param>
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            // If this is our first await, we're not boxed yet.  Set up our Task now, so it will be
            // visible to both the non-boxed and boxed builders.
            EnsureTaskCreated();
            AsyncMethodBuilderCore.CallOnCompleted(
                AsyncMethodBuilderCore.GetCompletionAction(ref m_moveNextAction, ref stateMachine, this.GetTaskIfDebuggingEnabled()),
                ref awaiter);
        }

        /// <summary>
        /// Schedules the specified state machine to be pushed forward when the specified awaiter completes.
        /// </summary>
        /// <typeparam name="TAwaiter">Specifies the type of the awaiter.</typeparam>
        /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
        /// <param name="awaiter">The awaiter.</param>
        /// <param name="stateMachine">The state machine.</param>
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            // If this is our first await, we're not boxed yet.  Set up our Task now, so it will be
            // visible to both the non-boxed and boxed builders.
            EnsureTaskCreated();
            AsyncMethodBuilderCore.CallUnsafeOnCompleted(
                AsyncMethodBuilderCore.GetCompletionAction(ref m_moveNextAction, ref stateMachine, this.GetTaskIfDebuggingEnabled()),
                ref awaiter);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureTaskCreated()
        {
            if (m_task == null)
                m_task = new Task<TResult>();
        }

        /// <summary>Gets the <see cref="System.Threading.Tasks.Task{TResult}"/> for this builder.</summary>
        /// <returns>The <see cref="System.Threading.Tasks.Task{TResult}"/> representing the builder's asynchronous operation.</returns>
        public Task<TResult> Task
        {
            get
            {
                // Get and return the task. If there isn't one, first create one and store it.
                var task = m_task;
                if (task == null) { m_task = task = new Task<TResult>(); }
                return task;
            }
        }

        /// <summary>
        /// Completes the <see cref="System.Threading.Tasks.Task{TResult}"/> in the 
        /// <see cref="System.Threading.Tasks.TaskStatus">RanToCompletion</see> state with the specified result.
        /// </summary>
        /// <param name="result">The result to use to complete the task.</param>
        /// <exception cref="System.InvalidOperationException">The task has already completed.</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetResult(TResult result)
        {
            // Get the currently stored task, which will be non-null if get_Task has already been accessed.
            // If there isn't one, get a task and store it.
            var task = m_task;
            if (task == null)
            {
                m_task = GetTaskForResult(result);
                Debug.Assert(m_task != null, "GetTaskForResult should never return null");
            }
            // Slow path: complete the existing task.
            else
            {
                if (DebuggerSupport.LoggingOn)
                    DebuggerSupport.TraceOperationCompletion(CausalityTraceLevel.Required, task, AsyncStatus.Completed);
                DebuggerSupport.RemoveFromActiveTasks(task);
                if (!task.TrySetResult(result))
                {
                    throw new InvalidOperationException(SR.TaskT_TransitionToFinal_AlreadyCompleted);
                }
            }
        }

        /// <summary>
        /// Completes the <see cref="System.Threading.Tasks.Task{TResult}"/> in the 
        /// <see cref="System.Threading.Tasks.TaskStatus">Faulted</see> state with the specified exception.
        /// </summary>
        /// <param name="exception">The <see cref="System.Exception"/> to use to fault the task.</param>
        /// <exception cref="System.ArgumentNullException">The <paramref name="exception"/> argument is null (Nothing in Visual Basic).</exception>
        /// <exception cref="System.InvalidOperationException">The task has already completed.</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetException(Exception exception)
        {
            AsyncTaskMethodBuilder.SetException(this.Task, exception);
        }

        /// <summary>
        /// Called by the debugger to request notification when the first wait operation
        /// (await, Wait, Result, etc.) on this builder's task completes.
        /// </summary>
        /// <param name="enabled">
        /// true to enable notification; false to disable a previously set notification.
        /// </param>
        /// <remarks>
        /// This should only be invoked from within an asynchronous method,
        /// and only by the debugger.
        /// </remarks>
        internal void SetNotificationForWaitCompletion(bool enabled)
        {
            // Get the task (forcing initialization if not already initialized), and set debug notification
            this.Task.SetNotificationForWaitCompletion(enabled);
        }

        /// <summary>
        /// Gets an object that may be used to uniquely identify this builder to the debugger.
        /// </summary>
        /// <remarks>
        /// This property lazily instantiates the ID in a non-thread-safe manner.  
        /// It must only be used by the debugger, and only in a single-threaded manner
        /// when no other threads are in the middle of accessing this property or this.Task.
        /// </remarks>
        private object ObjectIdForDebugger { get { return this.Task; } }

        /// <summary>
        /// Gets a task for the specified result.  This will either
        /// be a cached or new task, never null.
        /// </summary>
        /// <param name="result">The result for which we need a task.</param>
        /// <returns>The completed task containing the result.</returns>
        private static Task<TResult> GetTaskForResult(TResult result)
        {
            // Currently NUTC does not perform the optimization needed by this method.  The result is that
            // every call to this method results in quite a lot of work, including many allocations, which 
            // is the opposite of the intent.  For now, let's just return a new Task each time.
            // Bug 719350 tracks re-optimizing this in ProjectN.
#if false
            Contract.Ensures(
                EqualityComparer<TResult>.Default.Equals(result, Contract.Result<Task<TResult>>().Result),
                "The returned task's Result must return the same value as the specified result value.");

            // The goal of this function is to be give back a cached task if possible,
            // or to otherwise give back a new task.  To give back a cached task,
            // we need to be able to evaluate the incoming result value, and we need
            // to avoid as much overhead as possible when doing so, as this function
            // is invoked as part of the return path from every async method.
            // Most tasks won't be cached, and thus we need the checks for those that are 
            // to be as close to free as possible. This requires some trickiness given the 
            // lack of generic specialization in .NET.
            //
            // Be very careful when modifying this code.  It has been tuned
            // to comply with patterns recognized by both 32-bit and 64-bit JITs.
            // If changes are made here, be sure to look at the generated assembly, as
            // small tweaks can have big consequences for what does and doesn't get optimized away.
            //
            // Note that this code only ever accesses a static field when it knows it'll
            // find a cached value, since static fields (even if readonly and integral types) 
            // require special access helpers in this NGEN'd and domain-neutral.

            if (null != (object)default(TResult)) // help the JIT avoid the value type branches for ref types
            {
                // Special case simple value types:
                // - Boolean
                // - Byte, SByte
                // - Char
                // - Decimal
                // - Int32, UInt32
                // - Int64, UInt64
                // - Int16, UInt16
                // - IntPtr, UIntPtr
                // As of .NET 4.5, the (Type)(object)result pattern used below
                // is recognized and optimized by both 32-bit and 64-bit JITs.

                // For Boolean, we cache all possible values.
                if (typeof(TResult) == typeof(Boolean)) // only the relevant branches are kept for each value-type generic instantiation
                {
                    Boolean value = (Boolean)(object)result;
                    Task<Boolean> task = value ? AsyncTaskCache.TrueTask : AsyncTaskCache.FalseTask;
                    return (Task<TResult>)(Task)(task);// JitHelpers.UnsafeCast<Task<TResult>>(task); // UnsafeCast avoids type check we know will succeed
                }
                    // For Int32, we cache a range of common values, e.g. [-1,4).
                else if (typeof(TResult) == typeof(Int32))
                {
                    // Compare to constants to avoid static field access if outside of cached range.
                    // We compare to the upper bound first, as we're more likely to cache miss on the upper side than on the 
                    // lower side, due to positive values being more common than negative as return values.
                    Int32 value = (Int32)(object)result;
                    if (value < AsyncTaskCache.EXCLUSIVE_INT32_MAX && 
                        value >= AsyncTaskCache.INCLUSIVE_INT32_MIN)
                    {
                        Task<Int32> task = AsyncTaskCache.Int32Tasks[value - AsyncTaskCache.INCLUSIVE_INT32_MIN];
                        return (Task<TResult>)(Task)(task);// JitHelpers.UnsafeCast<Task<TResult>>(task); // UnsafeCast avoids a type check we know will succeed
                    }
                }
                    // For other known value types, we only special-case 0 / default(TResult).
                else if (
                    (typeof(TResult) == typeof(UInt32) && default(UInt32) == (UInt32)(object)result) ||
                    (typeof(TResult) == typeof(Byte) && default(Byte) == (Byte)(object)result) ||
                    (typeof(TResult) == typeof(SByte) && default(SByte) == (SByte)(object)result) ||
                    (typeof(TResult) == typeof(Char) && default(Char) == (Char)(object)result) ||
                    (typeof(TResult) == typeof(Decimal) && default(Decimal) == (Decimal)(object)result) ||
                    (typeof(TResult) == typeof(Int64) && default(Int64) == (Int64)(object)result) ||
                    (typeof(TResult) == typeof(UInt64) && default(UInt64) == (UInt64)(object)result) ||
                    (typeof(TResult) == typeof(Int16) && default(Int16) == (Int16)(object)result) ||
                    (typeof(TResult) == typeof(UInt16) && default(UInt16) == (UInt16)(object)result) ||
                    (typeof(TResult) == typeof(IntPtr) && default(IntPtr) == (IntPtr)(object)result) ||
                    (typeof(TResult) == typeof(UIntPtr) && default(UIntPtr) == (UIntPtr)(object)result))
                {
                    return s_defaultResultTask;
                }
            }
            else if (result == null) // optimized away for value types
            {
                return s_defaultResultTask;
            }
#endif

            // No cached task is available.  Manufacture a new one for this result.
            return new Task<TResult>(result);
        }
    }

    /// <summary>Provides a cache of closed generic tasks for async methods.</summary>
    internal static class AsyncTaskCache
    {
        // All static members are initialized inline to ensure type is beforefieldinit
#if false
        /// <summary>A cached Task{Boolean}.Result == true.</summary>
        internal readonly static Task<Boolean> TrueTask = CreateCacheableTask(true);
        /// <summary>A cached Task{Boolean}.Result == false.</summary>
        internal readonly static Task<Boolean> FalseTask = CreateCacheableTask(false);

        /// <summary>The cache of Task{Int32}.</summary>
        internal readonly static Task<Int32>[] Int32Tasks = CreateInt32Tasks();
        /// <summary>The minimum value, inclusive, for which we want a cached task.</summary>
        internal const Int32 INCLUSIVE_INT32_MIN = -1;
        /// <summary>The maximum value, exclusive, for which we want a cached task.</summary>
        internal const Int32 EXCLUSIVE_INT32_MAX = 9;
        /// <summary>Creates an array of cached tasks for the values in the range [INCLUSIVE_MIN,EXCLUSIVE_MAX).</summary>
        private static Task<Int32>[] CreateInt32Tasks()
        {
            Debug.Assert(EXCLUSIVE_INT32_MAX >= INCLUSIVE_INT32_MIN, "Expected max to be at least min");
            var tasks = new Task<Int32>[EXCLUSIVE_INT32_MAX - INCLUSIVE_INT32_MIN];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = CreateCacheableTask(i + INCLUSIVE_INT32_MIN);
            }
            return tasks;
        }
#endif

        /// <summary>Creates a non-disposable task.</summary>
        /// <typeparam name="TResult">Specifies the result type.</typeparam>
        /// <param name="result">The result for the task.</param>
        /// <returns>The cacheable task.</returns>
        internal static Task<TResult> CreateCacheableTask<TResult>(TResult result)
        {
            return new Task<TResult>(false, result, (TaskCreationOptions)InternalTaskOptions.DoNotDispose, default(CancellationToken));
        }
    }

    internal static class AsyncMethodBuilderCore
    {
        /// <summary>Initiates the builder's execution with the associated state machine.</summary>
        /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
        /// <param name="stateMachine">The state machine instance, passed by reference.</param>
        /// <exception cref="System.ArgumentNullException">The <paramref name="stateMachine"/> argument is null (Nothing in Visual Basic).</exception>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            // Async state machines are required not to throw, so no need for try/finally here.
            ExecutionContextSwitcher ecs = default(ExecutionContextSwitcher);
            ExecutionContext.EstablishCopyOnWriteScope(ref ecs);
            stateMachine.MoveNext();
            ecs.Undo();
        }

        //
        // We are going to do something odd here, which may require some explaining.  GetCompletionAction does quite a bit
        // of work, and is generic, parameterized over the type of the state machine.  Since every async method has its own
        // state machine type, and they are basically all value types, we end up generating separate copies of this method
        // for every state machine.  This adds up to a *lot* of code for an app that has many async methods.
        //
        // So, to save code size, we delegate all of the work to a non-generic helper. In the non-generic method, we have 
        // to coerce our "ref TStateMachine" arg into both a "ref byte" and a "ref IAsyncResult."
        //
        // Note that this is only safe because:
        //
        // a) We are coercing byrefs only.  These are just interior pointers; the runtime doesn't care *what* they point to.
        // b) We only read from one of those pointers after we're sure it's of the right type.  This prevents us from,
        //    say, ending up with a "heap reference" that's really a pointer to the stack. 
        //
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Action GetCompletionAction<TStateMachine>(ref Action cachedMoveNextAction, ref TStateMachine stateMachine, Task taskIfDebuggingEnabled)
            where TStateMachine : IAsyncStateMachine
        {
            return GetCompletionActionHelper(
                ref cachedMoveNextAction,
                ref Unsafe.As<TStateMachine, byte>(ref stateMachine),
                EETypePtr.EETypePtrOf<TStateMachine>(),
                taskIfDebuggingEnabled);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static unsafe Action GetCompletionActionHelper(
            ref Action cachedMoveNextAction,
            ref byte stateMachineAddress,
            EETypePtr stateMachineType,
            Task taskIfDebuggingEnabled)
        {
            // Alert a listening debugger that we can't make forward progress unless it slips threads.
            // If we don't do this, and a method that uses "await foo;" is invoked through funceval,
            // we could end up hooking up a callback to push forward the async method's state machine,
            // the debugger would then abort the funceval after it takes too long, and then continuing
            // execution could result in another callback being hooked up.  At that point we have
            // multiple callbacks registered to push the state machine, which could result in bad behavior.
            //Debugger.NotifyOfCrossThreadDependency();

            MoveNextRunner runner;
            if (cachedMoveNextAction != null)
            {
                Debug.Assert(cachedMoveNextAction.Target is MoveNextRunner);
                runner = (MoveNextRunner)cachedMoveNextAction.Target;
                runner.m_executionContext = ExecutionContext.Capture();
                return cachedMoveNextAction;
            }

            runner = new MoveNextRunner();
            runner.m_executionContext = ExecutionContext.Capture();
            cachedMoveNextAction = runner.CallMoveNext;

            if (taskIfDebuggingEnabled != null)
            {
                runner.m_task = taskIfDebuggingEnabled;

                if (DebuggerSupport.LoggingOn)
                {
                    IntPtr eeType = stateMachineType.RawValue;
                    DebuggerSupport.TraceOperationCreation(CausalityTraceLevel.Required, taskIfDebuggingEnabled, "Async: " + eeType.ToString("x"), 0);
                }
                DebuggerSupport.AddToActiveTasks(taskIfDebuggingEnabled);
            }

            //
            // If the state machine is a value type, we need to box it now.
            //
            IAsyncStateMachine boxedStateMachine;
            if (stateMachineType.IsValueType)
            {
                object boxed = RuntimeImports.RhBox(stateMachineType, ref stateMachineAddress);
                Debug.Assert(boxed is IAsyncStateMachine);
                boxedStateMachine = Unsafe.As<IAsyncStateMachine>(boxed);
            }
            else
            {
                boxedStateMachine = Unsafe.As<byte, IAsyncStateMachine>(ref stateMachineAddress);
            }

            runner.m_stateMachine = boxedStateMachine;

#if DEBUG
            //
            // In debug builds, we'll go ahead and call SetStateMachine, even though all of our initialization is done.
            // This way we'll keep forcing state machine implementations to keep the behavior needed by the CLR.
            //
            boxedStateMachine.SetStateMachine(boxedStateMachine);
#endif

            // All done!
            return cachedMoveNextAction;
        }

        private class MoveNextRunner
        {
            internal IAsyncStateMachine m_stateMachine;
            internal ExecutionContext m_executionContext;
            internal Task m_task;

            internal void CallMoveNext()
            {
                Task task = m_task;
                if (task != null)
                    DebuggerSupport.TraceSynchronousWorkStart(CausalityTraceLevel.Required, task, CausalitySynchronousWork.Execution);

                ExecutionContext.Run(
                    m_executionContext,
                    state => Unsafe.As<IAsyncStateMachine>(state).MoveNext(),
                    m_stateMachine);

                if (task != null)
                    DebuggerSupport.TraceSynchronousWorkCompletion(CausalityTraceLevel.Required, CausalitySynchronousWork.Execution);
            }
        }


        /// <summary>Associates the builder with the state machine it represents.</summary>
        /// <param name="stateMachine">The heap-allocated state machine object.</param>
        /// <exception cref="System.ArgumentNullException">The <paramref name="stateMachine"/> argument was null (Nothing in Visual Basic).</exception>
        /// <exception cref="System.InvalidOperationException">The builder is incorrectly initialized.</exception>
        internal static void SetStateMachine(IAsyncStateMachine stateMachine, Action cachedMoveNextAction)
        {
            //
            // Unlike the CLR, we do all of our initialization of the boxed state machine in GetCompletionAction.  All we 
            // need to do here is validate that everything's been set up correctly.  Note that we don't call 
            // IAsyncStateMachine.SetStateMachine in retail builds of the Framework, so we don't really expect a lot of calls
            // to this method.
            //
            if (stateMachine == null)
                throw new ArgumentNullException("stateMachine");
            Contract.EndContractBlock();
            if (cachedMoveNextAction == null)
                throw new InvalidOperationException(SR.AsyncMethodBuilder_InstanceNotInitialized);
            Action unwrappedMoveNextAction = TryGetStateMachineForDebugger(cachedMoveNextAction);
            if (unwrappedMoveNextAction.Target != stateMachine)
                throw new InvalidOperationException(SR.AsyncMethodBuilder_InstanceNotInitialized);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CallOnCompleted<TAwaiter>(Action continuation, ref TAwaiter awaiter)
            where TAwaiter : INotifyCompletion
        {
            try
            {
                awaiter.OnCompleted(continuation);
            }
            catch (Exception e)
            {
                RuntimeAugments.ReportUnhandledException(e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CallUnsafeOnCompleted<TAwaiter>(Action continuation, ref TAwaiter awaiter)
            where TAwaiter : ICriticalNotifyCompletion
        {
            try
            {
                awaiter.UnsafeOnCompleted(continuation);
            }
            catch (Exception e)
            {
                RuntimeAugments.ReportUnhandledException(e);
            }
        }

        /// <summary>Throws the exception on the ThreadPool.</summary>
        /// <param name="exception">The exception to propagate.</param>
        /// <param name="targetContext">The target context on which to propagate the exception.  Null to use the ThreadPool.</param>
        internal static void ThrowAsync(Exception exception, SynchronizationContext targetContext)
        {
            if (exception == null) throw new ArgumentNullException("exception");
            Contract.EndContractBlock();

            // If the user supplied a SynchronizationContext...
            if (targetContext != null)
            {
                try
                {
                    // Capture the exception into an ExceptionDispatchInfo so that its 
                    // stack trace and Watson bucket info will be preserved
                    var edi = ExceptionDispatchInfo.Capture(exception);

                    // Post the throwing of the exception to that context, and return.
                    targetContext.Post(state => ((ExceptionDispatchInfo)state).Throw(), edi);
                    return;
                }
                catch (Exception postException)
                {
                    // If something goes horribly wrong in the Post, we'll treat this a *both* exceptions
                    // going unhandled.
                    RuntimeAugments.ReportUnhandledException(new AggregateException(exception, postException));
                }
            }

            RuntimeAugments.ReportUnhandledException(exception);
        }

        //
        // This helper routine is targeted by the debugger. Its purpose is to remove any delegate wrappers introduced by the framework
        // that the debugger doesn't want to see.
        //
        [DependencyReductionRoot]
        internal static Action TryGetStateMachineForDebugger(Action action)
        {
            Object target = action.Target;
            MoveNextRunner runner = target as MoveNextRunner;
            if (runner != null)
                return runner.m_stateMachine.MoveNext;
            return action;
        }
    }
}
