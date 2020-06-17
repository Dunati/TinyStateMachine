using System;
using System.Collections.Generic;

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
    public static class TinyStateMachine<TState, TTrigger> {

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
                    return Guard == null ||
                     Guard(state, trigger, Next);
                }

                public TransitionBase(TState next) {
                    Next = next;
                }

            }
            internal class Transition : TransitionBase {
                public Action<TState, TTrigger, TState, TMemory> Action { get; set; }

                public Transition(TState next) : base(next) { }
            }

            internal class Transition<TArg> : TransitionBase {
                public Action<TState, TTrigger, TState, TMemory, TArg> Action { get; set; }

                public Transition(TState next) : base(next) { }
            }


            /// <summary>
            /// The compiler is used to generate the transitions for the state machine.
            /// </summary>
            public class Compiler {
                private readonly TState startingState;
                Action<TState, TTrigger, TState, TMemory> onAnyTransitionAction;

                private Dictionary<TState, Dictionary<TTrigger, TransitionBase>> transitions;

                private TState lastConfiguredState;
                private TTrigger lastConfiguredTrigger;

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
                /// <remarks>All transitions are owned by the result Machine and the state to the compiler is reset to that of a newly construted Compiler.</remarks>
                public Machine<TMemory> Compile() {

                    var machine = new Machine<TMemory>(startingState, transitions, onAnyTransitionAction);

                    transitions = null;
                    lastConfiguredState = default(TState);
                    lastConfiguredTrigger = default(TTrigger);
                    onAnyTransitionAction = null;
                    return machine;
                }


                /// <summary>
                /// See <see cref="Guard(Func&lt;TState,TTrigger,TState,bool&gt;)"/>.
                /// </summary>
                /// <param name="guard">A delegate to the method that will be called
                /// before attempting the transition.</param>
                /// <returns><c>this</c></returns>
                public Compiler Guard(Func<bool> guard) {
                    return Guard((f, tr, t) => guard());
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
                public Compiler Guard(
                    Func<TState, TTrigger, TState, bool> guard
                    ) {
                    if (guard == null) {
                        throw new ArgumentNullException("guard");
                    }

                    if (transitions.Count == 0) {
                        throw new InvalidOperationException(
                            "\"Guard\" cannot be called before configuring a" +
                            " transition."
                            );
                    }

                    var tr = transitions[lastConfiguredState][lastConfiguredTrigger];

                    if (tr.Guard != null) {
                        var errorMessage = string.Format(
                            "A guard has already been configured for state {0}" +
                            " and trigger {1}.",
                            lastConfiguredState,
                            lastConfiguredTrigger
                            );
                        throw new InvalidOperationException(errorMessage);
                    }

                    tr.Guard = guard;

                    return this;
                }


                /// <summary>
                /// See
                /// <see cref="On(Action{TState,TTrigger,TState,TMemory})"/>.
                /// </summary>
                /// <param name="action">A delegate to a method that will be called 
                /// on state change.</param>
                /// <returns><c>this</c></returns>
                public Compiler On<TArg>(Action<TMemory, TArg> action) {
                    return On<TArg>((f, tr, t, st, arg) => action(st, arg));
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
                public Compiler On<TArg>(
                    Action<TState, TTrigger, TState, TMemory, TArg> action
                    ) {
                    if (action == null) {
                        throw new ArgumentNullException("action");
                    }

                    if (transitions.Count == 0) {
                        throw new InvalidOperationException(
                            "\"On\" method cannot be called before configuring a" +
                            " transition."
                            );
                    }

                    var tr = transitions[lastConfiguredState][lastConfiguredTrigger] as Transition<TArg>;

                    if (tr == null) {
                        throw new InvalidOperationException($"{transitions[lastConfiguredState][lastConfiguredTrigger].GetType().Name} doesn't match expected type {typeof(Transition<TArg>).Name}");
                    }
                    if (tr.Action != null) {
                        var errorMessage = string.Format(
                            "An action has already been configured for state {0} and" +
                            " trigger {1}.",
                            lastConfiguredState,
                            lastConfiguredTrigger
                            );
                        throw new InvalidOperationException(errorMessage);
                    }

                    tr.Action = action;

                    return this;
                }



                /// <summary>
                /// See
                /// <see cref="On(Action{TState,TTrigger,TState,TMemory})"/>.
                /// </summary>
                /// <param name="action">A delegate to a method that will be called 
                /// on state change.</param>
                /// <returns><c>this</c></returns>
                public Compiler On(Action<TMemory> action) {
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
                public Compiler On(
                    Action<TState, TTrigger, TState, TMemory> action
                    ) {
                    if (action == null) {
                        throw new ArgumentNullException("action");
                    }

                    if (transitions.Count == 0) {
                        throw new InvalidOperationException(
                            "\"On\" method cannot be called before configuring a" +
                            " transition."
                            );
                    }

                    var tr = transitions[lastConfiguredState][lastConfiguredTrigger] as Transition;

                    if (tr == null) {
                        throw new InvalidOperationException($"{transitions[lastConfiguredState][lastConfiguredTrigger].GetType().Name} doesn't match expected type {typeof(Transition).Name}");
                    }
                    if (tr.Action != null) {
                        var errorMessage = string.Format(
                            "An action has already been configured for state {0} and" +
                            " trigger {1}.",
                            lastConfiguredState,
                            lastConfiguredTrigger
                            );
                        throw new InvalidOperationException(errorMessage);
                    }

                    tr.Action = action;

                    return this;
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
                public Compiler OnAny(
                    Action<TState, TTrigger, TState, TMemory> action
                    ) {
                    if (action == null) {
                        throw new ArgumentNullException("action");
                    }

                    onAnyTransitionAction = action;
                    return this;
                }


                private void validateTrArgs(TState from, TTrigger trigger, TState to) {
                    if (from == null) {
                        throw new ArgumentNullException("from");
                    }

                    if (trigger == null) {
                        throw new ArgumentNullException("trigger");
                    }

                    if (to == null) {
                        throw new ArgumentNullException("to");
                    }

                    if(transitions == null) {
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
                public Compiler Tr<TArg>(
                    TState from,
                    TTrigger trigger,
                    TState to
                    ) {
                    validateTrArgs(from, trigger, to);


                    var transtition = new Transition<TArg>(to);
                    transitions[from].Add(trigger, transtition);

                    lastConfiguredState = from;
                    lastConfiguredTrigger = trigger;

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
                public Compiler Tr(
                    TState from,
                    TTrigger trigger,
                    TState to
                    ) {
                    validateTrArgs(from, trigger, to);

                    var transtition = new Transition(to);
                    transitions[from].Add(trigger, transtition);

                    lastConfiguredState = from;
                    lastConfiguredTrigger = trigger;

                    return this;
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
                if (startingState == null) {
                    throw new ArgumentNullException("startingState");
                }

                this.startingState = startingState;
                this.transitions = transitions;
                this.onAnyTransitionAction = onAnyTransitionAction;
            }


            private void validateTrigger(TTrigger trigger, TState state) {
                if (trigger == null) {
                    throw new ArgumentNullException("trigger");
                }
                if (!transitions.ContainsKey(state)) {
                    var errorMessage = string.Format(
                        "There are no transitions configured for state \"{0}\""
                        , state
                        );

                    throw new InvalidOperationException(errorMessage);
                }

                if (!transitions[state].ContainsKey(trigger)) {
                    var errorMessage = string.Format(
                        "There are no transitions configured for state \"{0}\" " +
                        "and trigger \"{1}\"",
                        state,
                        trigger
                        );

                    throw new InvalidOperationException(errorMessage);
                }
            }


            private Transition<TArg> getTransition<TArg>(TState state, TTrigger trigger) {
                validateTrigger(trigger, state);
                var transition = transitions[state][trigger] as Transition<TArg>;
                if (transition == null) {
                    throw new InvalidOperationException($"{transitions[state][trigger].GetType().Name} doesn't match expected type {typeof(Transition<TArg>).Name}");
                }
                return transition;
            }
            private Transition getTransition(TState state, TTrigger trigger) {
                validateTrigger(trigger, state);
                var transition = transitions[state][trigger] as Transition;
                if (transition == null) {
                    throw new InvalidOperationException($"{transitions[state][trigger].GetType().Name} doesn't match expected type {typeof(Transition).Name}");
                }
                return transition;
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
            /// <em>not</em> fire any <see cref="Compiler.On(Action{TMemory})">transition
            /// events</see> and does <em>not</em> check any of the
            /// <see cref="Compiler.Guard(Func&lt;bool&gt;)">guard methods.</see>
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
                if (state == null) {
                    throw new ArgumentNullException("state");
                }
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
            /// <see cref="Compiler.On(Action{TMemory})">transition events</see> and does
            /// <em>not</em> check any of the 
            /// <see cref="Compiler.Guard(Func&lt;bool&gt;)">guard methods.</see>
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
