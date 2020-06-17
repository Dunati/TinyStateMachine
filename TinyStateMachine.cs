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

            private class Transition {
                public Action<TState, TTrigger, TState, TMemory> Action { get; set; }
                public Func<TState, TTrigger, TState, bool> Guard { get; set; }
                public TState Next { get; private set; }

                public Transition(TState next) {
                    Next = next;
                    Action = null;
                    Guard = null;
                }
            }


            #endregion Nested types

            /// <summary>
            /// Creates a new memory to store the state of the machine
            /// </summary>
            /// <returns></returns>
            public Storage CreateMemory() {
                canConfigure = false;
                return new Storage(startingState);
            }

            private readonly Dictionary<
                        TState,
                        Dictionary<TTrigger, Transition>
                        > transitions;

            private bool canConfigure;
            private TState lastConfiguredState;
            private TTrigger lastConfiguredTrigger;
            private readonly TState startingState;

            private Action<TState, TTrigger, TState, TMemory> onAnyTransitionAction;

            // private TState state;


            /// <summary>
            /// Initializes a new instance with the starting state given in
            /// <paramref name="startingState"/>
            /// </summary>
            /// <param name="startingState">The starting state of the FSM</param>
            /// <exception cref="System.ArgumentNullException">
            /// <paramref name="startingState"/> is null.
            /// </exception>
            public Machine(TState startingState) {
                if (startingState == null) {
                    throw new ArgumentNullException("startingState");
                }

                this.canConfigure = true;
                this.startingState = startingState;
                this.transitions =
                    new Dictionary<TState, Dictionary<TTrigger, Transition>>();
            }

            /// <summary>
            /// Transitions to a new state determined by <paramref name="trigger"/>
            /// and the configuration of the current state previously set by calls
            /// to one of the <see cref="Tr"/> methods.
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
                if (trigger == null) {
                    throw new ArgumentNullException("trigger");
                }
                var state = memory.Memory;
                canConfigure = false;
                if (!transitions.ContainsKey(state.state)) {
                    var errorMessage = string.Format(
                        "There are no transitions configured for state \"{0}\""
                        , state.state
                        );

                    throw new InvalidOperationException(errorMessage);
                }

                if (!transitions[state.state].ContainsKey(trigger)) {
                    var errorMessage = string.Format(
                        "There are no transitions configured for state \"{0}\" " +
                        "and trigger \"{1}\"",
                        state.state,
                        trigger
                        );

                    throw new InvalidOperationException(errorMessage);
                }

                var transition = transitions[state.state][trigger];

                var guardAllowsFiring =
                    transition.Guard == null ||
                    transition.Guard(state.state, trigger, transition.Next);

                if (!guardAllowsFiring) {
                    return;
                }

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
            /// See <see cref="Guard(Func&lt;TState,TTrigger,TState,bool&gt;)"/>.
            /// </summary>
            /// <param name="guard">A delegate to the method that will be called
            /// before attempting the transition.</param>
            /// <returns><c>this</c></returns>
            public Machine<TMemory> Guard(Func<bool> guard) {
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
            public Machine<TMemory> Guard(
                Func<TState, TTrigger, TState, bool> guard
                ) {
                if (guard == null) {
                    throw new ArgumentNullException("guard");
                }

                if (!canConfigure) {
                    throw new InvalidOperationException(
                        "\"Guard\" cannot be called after \"Fire()\" or" +
                        " \"Current\" are called."
                        );

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
            public Machine<TMemory> On(Action<TMemory> action) {
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
            public Machine<TMemory> On(
                Action<TState, TTrigger, TState, TMemory> action
                ) {
                if (action == null) {
                    throw new ArgumentNullException("action");
                }

                if (!canConfigure) {
                    throw new InvalidOperationException(
                        "\"On\" method cannot be called after \"Fire()\" or" +
                        " \"Current\" are called."
                        );
                }

                if (transitions.Count == 0) {
                    throw new InvalidOperationException(
                        "\"On\" method cannot be called before configuring a" +
                        " transition."
                        );
                }

                var tr = transitions[lastConfiguredState][lastConfiguredTrigger];
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
            public Machine<TMemory> OnAny(Action<TMemory> action) {
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
            public Machine<TMemory> OnAny(
                Action<TState, TTrigger, TState, TMemory> action
                ) {
                if (action == null) {
                    throw new ArgumentNullException("action");
                }

                if (!canConfigure) {
                    throw new InvalidOperationException(
                        "\"OnAny\" cannot be called after \"Fire()\" or" +
                        " \"Current\" are called."
                        );
                }

                onAnyTransitionAction = action;
                return this;
            }

            /// <summary>
            /// Sets the state of the machine to the starting state specified in
            /// the <see cref="Machine(TState)">constructor</see>, but 
            /// does <em>not</em> fire any 
            /// <see cref="On(Action{TMemory})">transition events</see> and does
            /// <em>not</em> check any of the 
            /// <see cref="Guard(Func&lt;bool&gt;)">guard methods.</see>
            /// </summary>
            public void Reset(TMemory memory) {
                memory.Memory.state = startingState;
            }

            /// <summary>
            /// Sets the state of the machine to <paramref name="state"/>, but does
            /// <em>not</em> fire any <see cref="On(Action{TMemory})">transition
            /// events</see> and does <em>not</em> check any of the
            /// <see cref="Guard(Func&lt;bool&gt;)">guard methods.</see>
            /// </summary>
            /// <param name="state">The state to which the machine will be set.
            /// </param>
            /// <param name="memory">The memory which stores the state.</param>
            /// <exception cref="System.ArgumentNullException">
            /// <paramref name="state"/> is null.
            /// </exception>
            /// <exception cref="System.InvalidOperationException">No 
            /// transitions are configured for given <paramref name="state"/>
            /// </exception>
            public void Reset(TState state, TMemory memory) {
                if (state == null) {
                    throw new ArgumentNullException("state");
                }

                if (!transitions.ContainsKey(state)) {
                    var errorMessage = string.Format(
                        "There are no transitions configured for state \"{0}\"",
                        state
                        );

                    throw new InvalidOperationException(errorMessage);
                }
                memory.Memory.state = state;
            }

            /// <summary>
            /// Short for "Transition." Adds a new entry to the state transition table.
            /// </summary>
            /// <param name="from">Current state</param>
            /// <param name="trigger">Trigger</param>
            /// <param name="to">The state the FSM will transition to.</param>
            /// <returns><c>this</c></returns>
            /// <exception cref="System.InvalidOperationException">If called after
            /// calling <see cref="Fire(TTrigger, TMemory)">Fire</see> or <see cref="Storage.State"/>
            /// </exception>
            /// <exception cref="System.ArgumentNullException">Any of the
            /// arguments <paramref name="from"/>, <paramref name="trigger"/>, or 
            /// <paramref name="to"/> is null.
            /// </exception>
            /// <remarks>
            /// <see cref="Tr"/> methods should be called after the
            /// <see cref="Machine(TState)">constructor</see> and
            /// <em>before</em> calling <see cref="Fire(TTrigger, TMemory)">Fire</see> or
            /// <see cref="Storage.State"/>. Attempting to call any of the <see cref="Tr"/>
            /// methods afterward will throw an
            /// <see cref="System.InvalidOperationException">
            /// InvalidOperationException</see>.
            /// </remarks>
            public Machine<TMemory> Tr(
                TState from,
                TTrigger trigger,
                TState to
                ) {
                if (from == null) {
                    throw new ArgumentNullException("from");
                }

                if (trigger == null) {
                    throw new ArgumentNullException("trigger");
                }

                if (to == null) {
                    throw new ArgumentNullException("to");
                }


                if (!canConfigure) {
                    throw new InvalidOperationException(
                        "\"Tr\" cannot be called after \"Fire()\" or \"Current\"" +
                        " are called."
                        );
                }

                if (!transitions.ContainsKey(from)) {
                    transitions.Add(from, new Dictionary<TTrigger, Transition>());
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

                var transtition = new Transition(to);
                transitions[from].Add(trigger, transtition);

                lastConfiguredState = from;
                lastConfiguredTrigger = trigger;

                return this;
            }
        }
    }
}
