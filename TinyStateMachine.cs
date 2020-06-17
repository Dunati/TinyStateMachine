using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace M16h {


    /// <summary>
    /// Static class which groups related state machine classes.
    /// </summary>
    /// <typeparam name="TState">
    /// The type representing all possible states of the FSM.
    /// </typeparam>
    /// <typeparam name="TTrigger">
    /// The type representing the triggers that cause state transitions.
    /// </typeparam>
    public static class TinyStateMachine<TState, TTrigger>
        where TTrigger : struct, Enum
        where TState : struct, Enum {

        [Conditional("Debug")]
        private static void Assert<TException>(bool value, string message) where TException : Exception {
            if (!value) {
                throw (Exception)Activator.CreateInstance(typeof(TException), message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public interface IStorage {
            /// <summary>
            /// Gets the state held by this interface.
            /// </summary>
            Storage Memory { get; }
        }

        /// <summary>
        /// Holds the memory for one instance. This class can be used directly if no additional information is 
        /// required to go along with the state. To tie an class to the state, have that class implement <see cref="IStorage"/>.
        /// </summary>
        public class Storage : IStorage {
            internal TState state;

            internal Storage(TState state) {
                this.state = state;
            }


            /// <summary>
            /// Gets the state held by this interface.
            /// </summary>
            public Storage Memory => this;

            /// <summary>
            /// The current state of the FSM.
            /// </summary>
            public TState State {
                get {
                    return state;
                }
            }
            /// <summary>
            /// Returns true if machine is in <c>state</c>. Not very useful for
            /// now, but should come in handy if support for sub-states is ever
            /// added.
            /// </summary>
            /// <param name="state">The <c>state</c> to test for.</param>
            /// <returns><c>true</c> if in <paramref name="state"/>, false
            /// otherwise
            /// </returns>
            public bool IsInState(TState state) {
                return this.State.Equals(state);
            }
        }



        /// <summary>
        /// A simple
        /// <a href="http://en.wikipedia.org/wiki/Finite-state_machine">
        /// finite-state machine (FSM)</a> that uses state transition tables for
        /// configuration.
        /// </summary>
        /// <typeparam name="TMemory">
        /// The type representing the memory state.
        /// </typeparam>
        public class Machine<TMemory> where TMemory : IStorage {
            #region Nested types

            /// <summary>
            /// Base class for transitions, should not be instantiated
            /// </summary>
            internal abstract class TransitionBase {
                public Func<TState, TTrigger, TState, bool> Guard { get; set; }
                public TState Next { get; }

                public bool Allowed(TState state, TTrigger trigger) {
                    return
                        Guard == null
                        || Guard(state, trigger, Next);
                }

                public TransitionBase(TState next) {
                    Next = next;
                }

                public override string ToString() {
                    return $"Transition to {Next} Guard:{Guard != null}";
                }
            }
            internal class Transition : TransitionBase {
                public Action<TState, TTrigger, TState, TMemory> Action { get; set; }

                public Transition(TState next) : base(next) { }

                public override string ToString() {
                    return $"{base.ToString()} Action:{Action != null}";
                }
            }

            internal class Transition<TArg> : TransitionBase {
                public Action<TState, TTrigger, TState, TMemory, TArg> Action { get; set; }

                public Transition(TState next) : base(next) { }
                public override string ToString() {
                    return $"{base.ToString()} Action:{Action != null}";
                }
            }


            /// <summary>
            /// The compiler is used to generate the transitions for the state machine.
            /// </summary>
            public class Compiler {
                private readonly TState startingState;
                Action<TState, TTrigger, TState, TMemory> onAnyTransitionAction;

                private Dictionary<TState, Dictionary<TTrigger, TransitionBase>> transitions;

                /// <summary>
                /// Initializes a new Compiler
                /// </summary>
                /// <param name="startingState">The initial state for new <see cref="Storage"/> objects.</param>
                public Compiler(TState startingState) {
                    this.startingState = startingState;
                }

                /// <summary>
                /// Creates a new Machine.
                /// </summary>
                /// <returns>A new Machine with the configured transitions.</returns>
                /// <remarks>All transitions are owned by the result Machine and the state to the compiler is reset to that of a newly constructed Compiler.</remarks>
                public Machine<TMemory> Compile() {

                    var machine = new Machine<TMemory>(startingState, transitions, onAnyTransitionAction);

                    transitions = null;
                    onAnyTransitionAction = null;
                    return machine;
                }

                /// <summary>
                /// Fluent interface for creating transitions with an argument
                /// </summary>
                public class TransitionCompiler<TArg> {
                    internal readonly Transition<TArg> transition;
                    /// <summary>
                    /// The source state for this transition
                    /// </summary>
                    public readonly TState sourceState;

                    /// <summary>
                    /// The reference to the compiler that created this Transition
                    /// </summary>
                    public readonly Compiler compiler;

                    internal TransitionCompiler(TState state, Transition<TArg> transition, Compiler compiler) {
                        this.transition = transition;
                        this.sourceState = state;
                        this.compiler = compiler;
                    }

                    /// <summary>
                    /// Creates a new Machine.
                    /// </summary>
                    /// <returns>A new Machine with the configured transitions.</returns>
                    /// <remarks>All transitions are owned by the result Machine and the state to the compiler is reset to that of a newly construted Compiler.</remarks>
                    public Machine<TMemory> Compile() {
                        return compiler.Compile();
                    }

                    /// <summary>
                    /// Sets the method that will be called <em>before</em> attempting to make the
                    /// transition described by the last call to 
                    /// <see cref="Tr(TState, TTrigger, TState)">Tr</see>. The transitions
                    /// will be silently aborted without throwing any errors if
                    /// <paramref name="guard"/> method returns <c>false</c>, and will
                    /// continue normally if the method returns <c>true</c>
                    /// </summary>
                    /// <param name="guard">A delegate to the method that will be called
                    /// before attempting the transition.</param>
                    /// <returns><c>this</c></returns>
                    /// <exception cref="System.InvalidOperationException">No transition
                    /// was configured before calling this method or a guard was already
                    /// set for the last transition.
                    /// </exception>
                    /// <exception cref="System.ArgumentNullException">
                    /// <paramref name="guard"/> is null.
                    /// </exception>
                    public TransitionCompiler<TArg> Guard(Func<TState, TTrigger, TState, bool> guard) {
                        Assert<ArgumentNullException>(guard != null, "Guard func was null");
                        Assert<InvalidOperationException>(transition.Action == null, $"A guard has already been configured for {sourceState} {transition}");

                        transition.Guard = guard;
                        return this;
                    }


                    /// <summary>
                    /// See <see cref="Guard(Func&lt;TState,TTrigger,TState,bool&gt;)"/>.
                    /// </summary>
                    /// <param name="guard">A delegate to the method that will be called
                    /// before attempting the transition.</param>
                    /// <returns><c>this</c></returns>
                    public TransitionCompiler<TArg> Guard(Func<bool> guard) {
                        return Guard((f, tr, t) => guard());
                    }

                    /// <summary>
                    /// See
                    /// <see cref="On(Action{TState, TTrigger, TState, TMemory, TArg})"/>.
                    /// </summary>
                    /// <param name="action">A delegate to a method that will be called 
                    /// on state change.</param>
                    /// <returns><c>this</c></returns>
                    public TransitionCompiler<TArg> On(Action<TMemory, TArg> action) {
                        return On((f, tr, t, st, arg) => action(st, arg));
                    }
                    /// <summary>
                    /// Sets the action that will be called <em>after</em> the transition
                    /// described by the last call to 
                    /// <see cref="Tr(TState, TTrigger, TState)">Tr</see> takes place.
                    /// </summary>
                    /// <param name="action">A delegate to a method that will be called 
                    /// on state change.</param>
                    /// <returns><c>this</c></returns>
                    /// <exception cref="System.InvalidOperationException">No transition
                    /// was configured before calling this method, an action was already
                    /// set for the last transition, or the method was called after the 
                    /// the configuration phase was done (i.e. after 
                    /// <see cref="Fire(TTrigger, TMemory)">Fire</see>, <see cref="IStorage"/>, or
                    /// <see cref="Storage.IsInState(TState)">IsInState</see>) were called).
                    /// </exception>
                    /// <exception cref="System.ArgumentNullException">
                    /// <paramref name="action"/> is null.
                    /// </exception>
                    public TransitionCompiler<TArg> On(Action<TState, TTrigger, TState, TMemory, TArg> action) {
                        Assert<ArgumentNullException>(action != null, "Action func was null");
                        Assert<InvalidOperationException>(transition.Action == null, $"An action has already been configured for {sourceState} {transition}");

                        transition.Action = action;
                        return this;
                    }

                    /// <summary>
                    /// Short for "Transition." Adds a new entry to the state transition table.
                    /// </summary>
                    /// <param name="from">Current state</param>
                    /// <param name="trigger">Trigger</param>
                    /// <param name="to">The state the FSM will transition to.</param>
                    /// <returns><c>this</c></returns>
                    /// <exception cref="System.ArgumentNullException">Any of the
                    /// arguments <paramref name="from"/>, <paramref name="trigger"/>, or 
                    /// <paramref name="to"/> is null.
                    /// </exception>
                    public TransitionCompiler<TNewArg> Tr<TNewArg>(TState from, TTrigger trigger, TState to) {
                        return compiler.Tr<TNewArg>(from, trigger, to);
                    }


                    /// <summary>
                    /// Short for "Transition." Adds a new entry to the state transition table.
                    /// </summary>
                    /// <param name="from">Current state</param>
                    /// <param name="trigger">Trigger</param>
                    /// <param name="to">The state the FSM will transition to.</param>
                    /// <returns><c>this</c></returns>
                    /// <exception cref="System.ArgumentNullException">Any of the
                    /// arguments <paramref name="from"/>, <paramref name="trigger"/>, or 
                    /// <paramref name="to"/> is null.
                    /// </exception>
                    public TransitionCompiler Tr(TState from, TTrigger trigger, TState to) {
                        return compiler.Tr(from, trigger, to);
                    }
                }

                /// <summary>
                /// Fluent interface for creating transitions
                /// </summary>
                public class TransitionCompiler {
                    internal readonly Transition transition;
                    /// <summary>
                    /// The source state for this transition
                    /// </summary>
                    public readonly TState sourceState;

                    /// <summary>
                    /// The reference to the compiler that created this Transition
                    /// </summary>
                    public readonly Compiler compiler;

                    internal TransitionCompiler(TState state, Transition transition, Compiler compiler) {
                        this.transition = transition;
                        this.sourceState = state;
                        this.compiler = compiler;
                    }

                    /// <summary>
                    /// Creates a new Machine.
                    /// </summary>
                    /// <returns>A new Machine with the configured transitions.</returns>
                    /// <remarks>All transitions are owned by the result Machine and the state to the compiler is reset to that of a newly construted Compiler.</remarks>
                    public Machine<TMemory> Compile() {
                        return compiler.Compile();
                    }



                    /// <summary>
                    /// Sets the method that will be called <em>before</em> attempting to make the
                    /// transition described by the last call to 
                    /// <see cref="Tr(TState, TTrigger, TState)">Tr</see>. The transitions
                    /// will be silently aborted without throwing any errors if
                    /// <paramref name="guard"/> method returns <c>false</c>, and will
                    /// continue normally if the method returns <c>true</c>
                    /// </summary>
                    /// <param name="guard">A delegate to the method that will be called
                    /// before attempting the transition.</param>
                    /// <returns><c>this</c></returns>
                    /// <exception cref="System.InvalidOperationException">No transition
                    /// was configured before calling this method or a guard was already
                    /// set for the last transition.
                    /// </exception>
                    /// <exception cref="System.ArgumentNullException">
                    /// <paramref name="guard"/> is null.
                    /// </exception>
                    public TransitionCompiler Guard(Func<TState, TTrigger, TState, bool> guard) {
                        Assert<ArgumentNullException>(guard != null, "Guard func was null");
                        Assert<InvalidOperationException>(transition.Action == null, $"A guard has already been configured for {sourceState} {transition}");

                        transition.Guard = guard;
                        return this;
                    }


                    /// <summary>
                    /// See <see cref="Guard(Func&lt;TState,TTrigger,TState,bool&gt;)"/>.
                    /// </summary>
                    /// <param name="guard">A delegate to the method that will be called
                    /// before attempting the transition.</param>
                    /// <returns><c>this</c></returns>
                    public TransitionCompiler Guard(Func<bool> guard) {
                        return Guard((f, tr, t) => guard());
                    }

                    /// <summary>
                    /// See
                    /// <see cref="On(Action{TState,TTrigger,TState,TMemory})"/>.
                    /// </summary>
                    /// <param name="action">A delegate to a method that will be called 
                    /// on state change.</param>
                    /// <returns><c>this</c></returns>
                    public TransitionCompiler On(Action<TMemory> action) {
                        return On((f, tr, t, st) => action(st));
                    }
                    /// <summary>
                    /// Sets the action that will be called <em>after</em> the transition
                    /// described by the last call to 
                    /// <see cref="Tr(TState, TTrigger, TState)">Tr</see> takes place.
                    /// </summary>
                    /// <param name="action">A delegate to a method that will be called 
                    /// on state change.</param>
                    /// <returns><c>this</c></returns>
                    /// <exception cref="System.InvalidOperationException">No transition
                    /// was configured before calling this method, an action was already
                    /// set for the last transition, or the method was called after the 
                    /// the configuration phase was done (i.e. after 
                    /// <see cref="Fire(TTrigger, TMemory)">Fire</see>, <see cref="IStorage"/>, or
                    /// <see cref="Storage.IsInState(TState)">IsInState</see>) were called).
                    /// </exception>
                    /// <exception cref="System.ArgumentNullException">
                    /// <paramref name="action"/> is null.
                    /// </exception>
                    public TransitionCompiler On(Action<TState, TTrigger, TState, TMemory> action) {
                        Assert<ArgumentNullException>(action != null, "Action func was null");
                        Assert<InvalidOperationException>(transition.Action == null, $"An action has already been configured for {sourceState} {transition}");

                        transition.Action = action;

                        return this;
                    }


                    /// <summary>
                    /// Short for "Transition." Adds a new entry to the state transition table.
                    /// </summary>
                    /// <param name="from">Current state</param>
                    /// <param name="trigger">Trigger</param>
                    /// <param name="to">The state the FSM will transition to.</param>
                    /// <returns><c>this</c></returns>
                    /// <exception cref="System.ArgumentNullException">Any of the
                    /// arguments <paramref name="from"/>, <paramref name="trigger"/>, or 
                    /// <paramref name="to"/> is null.
                    /// </exception>
                    public TransitionCompiler<TArg> Tr<TArg>(TState from, TTrigger trigger, TState to) {
                        return compiler.Tr<TArg>(from, trigger, to);
                    }


                    /// <summary>
                    /// Short for "Transition." Adds a new entry to the state transition table.
                    /// </summary>
                    /// <param name="from">Current state</param>
                    /// <param name="trigger">Trigger</param>
                    /// <param name="to">The state the FSM will transition to.</param>
                    /// <returns><c>this</c></returns>
                    /// <exception cref="System.ArgumentNullException">Any of the
                    /// arguments <paramref name="from"/>, <paramref name="trigger"/>, or 
                    /// <paramref name="to"/> is null.
                    /// </exception>
                    public TransitionCompiler Tr(TState from, TTrigger trigger, TState to) {
                        return compiler.Tr(from, trigger, to);
                    }

                }



                /// <summary>
                /// See <see cref="OnAny(Action{TState,TTrigger,TState,TMemory})"/>.
                /// </summary>
                /// <param name="action">A delegate to a method that will be called 
                /// on state change.</param>
                /// <returns><c>this</c></returns>
                public Compiler OnAny(Action<TMemory> action) {
                    return OnAny((f, tr, t, st) => action(st));
                }

                /// <summary>
                /// Sets the action that will be called after <em>any</em> finite state
                /// machine transition is triggered.
                /// </summary>
                /// <param name="action">A delegate to a method that will be called 
                /// on state change.</param>
                /// <returns><c>this</c></returns>
                /// <exception cref="System.InvalidOperationException">The method was
                /// called after the configuration phase was done (i.e. after 
                /// <see cref="Fire(TTrigger, TMemory)">Fire</see>, <see cref="IStorage"/>, or
                /// <see cref="Storage.IsInState(TState)">IsInState</see>) were called).
                /// </exception>
                /// <exception cref="System.ArgumentNullException">
                /// <paramref name="action"/> is null.
                /// </exception>
                public Compiler OnAny(Action<TState, TTrigger, TState, TMemory> action) {
                    Assert<ArgumentNullException>(action != null, "OnAny action func was null");

                    onAnyTransitionAction = action;
                    return this;
                }


                private void validateTrArgs(TState from, TTrigger trigger, TState to) {

                    if (transitions == null) {
                        transitions = new Dictionary<TState, Dictionary<TTrigger, TransitionBase>>();
                    }

                    if (!transitions.ContainsKey(from)) {
                        transitions.Add(from, new Dictionary<TTrigger, TransitionBase>());
                    }
                    else if (transitions[from].ContainsKey(trigger)) {
                        string errorMessage = string.Format(
                            "A transition is already defined for state {0} and" +
                            " trigger {1}",
                            from,
                            trigger
                            );
                        throw new InvalidOperationException(errorMessage);
                    }
                }

                /// <summary>
                /// Short for "Transition." Adds a new entry to the state transition table.
                /// </summary>
                /// <param name="from">Current state</param>
                /// <param name="trigger">Trigger</param>
                /// <param name="to">The state the FSM will transition to.</param>
                /// <returns><c>this</c></returns>
                /// <exception cref="System.ArgumentNullException">Any of the
                /// arguments <paramref name="from"/>, <paramref name="trigger"/>, or 
                /// <paramref name="to"/> is null.
                /// </exception>
                public TransitionCompiler<TArg> Tr<TArg>(TState from, TTrigger trigger, TState to) {
                    validateTrArgs(from, trigger, to);

                    var transtition = new Transition<TArg>(to);
                    transitions[from].Add(trigger, transtition);

                    return new TransitionCompiler<TArg>(from, transtition, this);
                }


                /// <summary>
                /// Short for "Transition." Adds a new entry to the state transition table.
                /// </summary>
                /// <param name="from">Current state</param>
                /// <param name="trigger">Trigger</param>
                /// <param name="to">The state the FSM will transition to.</param>
                /// <returns><c>this</c></returns>
                /// <exception cref="System.ArgumentNullException">Any of the
                /// arguments <paramref name="from"/>, <paramref name="trigger"/>, or 
                /// <paramref name="to"/> is null.
                /// </exception>
                public TransitionCompiler Tr(TState from, TTrigger trigger, TState to) {
                    validateTrArgs(from, trigger, to);

                    var transition = new Transition(to);
                    transitions[from].Add(trigger, transition);

                    return new TransitionCompiler(from, transition, this);
                }
            }


            #endregion Nested types

            /// <summary>
            /// Creates a new memory to store the state of the machine
            /// </summary>
            /// <returns></returns>
            public Storage CreateMemory() {
                return new Storage(startingState);
            }

            private readonly TState startingState;
            private readonly Dictionary<TState, Dictionary<TTrigger, TransitionBase>> transitions;
            private readonly Action<TState, TTrigger, TState, TMemory> onAnyTransitionAction;

            internal Machine(TState startingState,
                Dictionary<TState, Dictionary<TTrigger, TransitionBase>> transitions,
                Action<TState, TTrigger, TState, TMemory> onAnyTransitionAction) {

                this.startingState = startingState;
                this.transitions = transitions;
                this.onAnyTransitionAction = onAnyTransitionAction;
            }

            private Transition<TArg> getTransition<TArg>(TState state, TTrigger trigger) {
                if (transitions.TryGetValue(state, out var table)) {
                    if (table.TryGetValue(trigger, out var transitionBase)) {
                        var transition = transitionBase as Transition<TArg>;
                        if (transition == null) {
                            throw new InvalidOperationException($"Fire expected a transition with <{typeof(TArg)}> argument, actual type was <{transitionBase.GetType().GetGenericArguments().Skip(3).FirstOrDefault()}>");
                        }
                        return transition;
                    }
                    else {
                        throw new InvalidOperationException($"There are no transitions configured for state '{state}' with trigger '{trigger}'");
                    }
                }
                else {
                    throw new InvalidOperationException($"There are no transitions configured for state '{state}'");
                }
            }
            private Transition getTransition(TState state, TTrigger trigger) {
                if (transitions.TryGetValue(state, out var table)) {
                    if (table.TryGetValue(trigger, out var transitionBase)) {
                        var transition = transitionBase as Transition;
                        if (transition == null) {
                            throw new InvalidOperationException($"Fire expected a transition with <{null}> argument, actual type was <{transitionBase.GetType().GetGenericArguments().Skip(3).FirstOrDefault()}>");
                        }
                        return transition;
                    }
                    else {
                        throw new InvalidOperationException($"There are no transitions configured for state '{state}' with trigger '{trigger}'");
                    }
                }
                else {
                    throw new InvalidOperationException($"There are no transitions configured for state '{state}'");
                }
            }


            private void transitionState<Targ>(Storage state, Transition<Targ> transition, TTrigger trigger, TMemory memory, Targ arg) {
                var currentState = state.state;
                var nextState = transition.Next;
                state.state = transition.Next;

                if (transition.Action != null) {
                    transition.Action(currentState, trigger, nextState, memory, arg);
                }

                if (onAnyTransitionAction != null) {
                    onAnyTransitionAction(currentState, trigger, nextState, memory);
                }
            }
            private void transitionState(Storage state, Transition transition, TTrigger trigger, TMemory memory) {
                var currentState = state.state;
                var nextState = transition.Next;
                state.state = transition.Next;

                if (transition.Action != null) {
                    transition.Action(currentState, trigger, nextState, memory);
                }

                if (onAnyTransitionAction != null) {
                    onAnyTransitionAction(currentState, trigger, nextState, memory);
                }
            }


            /// <summary>
            /// Transitions to a new state determined by <paramref name="trigger"/>
            /// and the configuration of the current state previously set by calls
            /// to one of the <see cref="Compiler.Tr"/> methods.
            /// </summary>
            /// <param name="trigger">The trigger to fire.</param>
            /// <param name="memory">Storage holding the state.</param>
            /// <param name="arg">Argument to the on method</param>
            /// <exception cref="System.InvalidOperationException">No transition
            /// was configured for <paramref name="trigger"/> and the current
            /// state.
            /// </exception>
            /// <exception cref="System.ArgumentNullException">
            /// <paramref name="trigger"/> is null.
            /// </exception>
            public void Fire<Targ>(TTrigger trigger, TMemory memory, Targ arg) {
                var state = memory.Memory;
                var transition = getTransition<Targ>(state.state, trigger);

                if (!transition.Allowed(state.state, trigger)) {
                    return;
                }

                transitionState(state, transition, trigger, memory, arg);

            }

            /// <summary>
            /// Transitions to a new state determined by <paramref name="trigger"/>
            /// and the configuration of the current state previously set by calls
            /// to one of the <see cref="Compiler.Tr"/> methods.
            /// </summary>
            /// <param name="trigger">The trigger to fire.</param>
            /// <param name="memory">Storage holding the state.</param>
            /// <exception cref="System.InvalidOperationException">No transition
            /// was configured for <paramref name="trigger"/> and the current
            /// state.
            /// </exception>
            /// <exception cref="System.ArgumentNullException">
            /// <paramref name="trigger"/> is null.
            /// </exception>
            public void Fire(TTrigger trigger, TMemory memory) {
                if (memory == null) {
                    throw new ArgumentNullException("memory");
                }
                var state = memory.Memory;
                var transition = getTransition(state.state, trigger);

                if (!transition.Allowed(state.state, trigger)) {
                    return;
                }

                transitionState(state, transition, trigger, memory);
            }


            /// <summary>
            /// Sets the state of the machine to <paramref name="state"/>, but does
            /// <em>not</em> fire any <see cref="M:Compiler.TransitionCompiler.On">transition
            /// events</see> and does <em>not</em> check any of the
            /// <see cref="M:Compiler.TransitionCompiler.Guard">guard methods.</see>
            /// </summary>
            /// <param name="state">The state to which the machine will be set.
            /// </param>
            /// <param name="memory">The memory which stores the state.</param>
            /// <exception cref="System.ArgumentNullException">
            /// <paramref name="state"/> is null or <paramref name="memory"/> is null.
            /// </exception>
            /// <exception cref="System.InvalidOperationException">No 
            /// transitions are configured for given <paramref name="state"/>
            /// </exception>
            public void Reset(TState state, TMemory memory) {
                if (memory == null) {
                    throw new ArgumentNullException("memory");
                }

                if (!transitions.ContainsKey(state)) {
                    throw new InvalidOperationException($"There are no transitions configured for state '{state}'");
                }
                memory.Memory.state = state;
            }


            /// <summary>
            /// Sets the state of the machine to the starting state specified in
            /// the <see cref="Compiler(TState)">constructor</see>, but 
            /// does <em>not</em> fire any 
            /// <see cref="M:Compiler.TransitionCompiler.On">transition events</see> and does
            /// <em>not</em> check any of the 
            /// <see cref="M:Compiler.TransitionCompiler.Guard">guard methods.</see>
            /// </summary>
            /// <exception cref="System.ArgumentNullException">
            /// <paramref name="memory"/> is null.
            /// </exception>
            public void Reset(TMemory memory) {
                if (memory == null) {
                    throw new ArgumentNullException("memory");
                }
                memory.Memory.state = startingState;
            }
        }
    }
}
