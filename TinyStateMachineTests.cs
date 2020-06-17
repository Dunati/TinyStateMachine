using System;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

using StateToken = M16h.StateToken<M16h.TinyStateMachineTests.DoorState, M16h.TinyStateMachineTests.DoorEvents>;
using IStateToken = M16h.IStateToken<M16h.TinyStateMachineTests.DoorState, M16h.TinyStateMachineTests.DoorEvents>;
using Tsm = M16h.TinyStateMachine<M16h.TinyStateMachineTests.DoorState, M16h.TinyStateMachineTests.DoorEvents, M16h.StateToken<M16h.TinyStateMachineTests.DoorState, M16h.TinyStateMachineTests.DoorEvents>>;

namespace M16h {

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// A test for TinyStateMachine using the canonical door example. Door
    /// can be in one of two states: either <see cref="DoorState.Open">Open</see>
    /// or <see cref="DoorState.Closed">closed</see>, represented by the 
    /// <see cref="DoorState"/> enum. State of the door can only be changed
    /// by receiving either one of two events: <see cref="DoorEvents.Open"/> 
    /// or <see cref="DoorEvents.Close"/>.
    /// </summary>
    [TestFixture]
    public class TinyStateMachineTests {
        internal enum DoorState {
            Closed,
            Open,
        }

        internal enum DoorEvents {
            Open,
            Close,
        }


        private static Tsm GetFixture() {
            return GetFixture<StateToken<DoorState, DoorEvents>>();
        }

        private static TinyStateMachine<DoorState, DoorEvents, TToken> GetFixture<TToken>() where TToken : IStateToken<DoorState, DoorEvents> {
            var machine = new TinyStateMachine<DoorState, DoorEvents, TToken>(
                DoorState.Closed
                );
            machine.Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                   .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed);
            return machine;
        }

        const int ParallelTestCount = 100;
        private static StateToken[] GetTokens(Tsm machine) {
            var tokens = new StateToken[ParallelTestCount];

            for (int i = 0; i < ParallelTestCount; i++) {
                tokens[i] = machine.CreateToken();
            }
            return tokens;
        }

        [Test]
        public void State_machine_is_constructed_with_the_correct_initial_state() {
            var machine = GetFixture();

            Parallel.ForEach(GetTokens(machine), token => {
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
            });
        }

        [Test]
        public void Test_simple_transition() {
            var machine = GetFixture();

            Parallel.ForEach(GetTokens(machine), token => {
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, token);
                Assert.That(token.State, Is.EqualTo(DoorState.Open));
            });
        }

        [Test]
        public void Test_simple_transition_2_tokens() {
            var machine = GetFixture();

            Parallel.ForEach(GetTokens(machine), token => {
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, token);
                Assert.That(token.State, Is.EqualTo(DoorState.Open));
            });
        }

        class DoorMemory : IStateToken {
            internal bool wasDoorOpened = false;
            internal bool wasDoorClosed = false;

            internal DoorMemory(M16h.TinyStateMachine<M16h.TinyStateMachineTests.DoorState, M16h.TinyStateMachineTests.DoorEvents, DoorMemory> stateMachine) {
                Token = stateMachine.CreateToken();
            }

            public StateToken Token { get; private set; }
        }

        [Test]
        public void Appropriate_action_is_called_on_transition() {

            var machine = new TinyStateMachine<TinyStateMachineTests.DoorState, TinyStateMachineTests.DoorEvents, DoorMemory>(
                DoorState.Closed
                );

            machine.Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                   .On((t) => t.wasDoorOpened = true)
                   .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed)
                   .On((t) => t.wasDoorClosed = true);

            var doorMemory = new DoorMemory[ParallelTestCount];
            for (int i = 0; i < ParallelTestCount; i++) {
                doorMemory[i] = new DoorMemory(machine);
            }

            Parallel.ForEach(doorMemory, token => {
                Assert.That(token.wasDoorOpened, Is.False);
                Assert.That(token.wasDoorClosed, Is.False);

                machine.Fire(DoorEvents.Open, token);

                Assert.That(token.wasDoorOpened, Is.True);
                Assert.That(token.wasDoorClosed, Is.False);

                machine.Fire(DoorEvents.Close, token);

                Assert.That(token.wasDoorOpened, Is.True);
                Assert.That(token.wasDoorClosed, Is.True);
            });
        }

        [Test]
        public void Firing_trigger_with_no_valid_transition_throws_exception() {
            var machine = GetFixture();
            var token = machine.CreateToken();
            Assert.Throws<InvalidOperationException>(
                () => machine.Fire(DoorEvents.Close, token)
                );
        }

        [Test]
        public void Guard_can_stop_transition() {
            var machine = new Tsm(
                DoorState.Closed
                );
            machine.Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                   .Guard(() => false)
                   .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed);

            Parallel.ForEach(GetTokens(machine), token => {
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, token);
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
            });
        }

        [Test]
        public void Action_is_called_with_the_expected_parameters() {
            var machine = new Tsm(
                DoorState.Closed
                );
            machine.Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                   .On((from, trigger, to, token) => {
                       Assert.That(from, Is.EqualTo(DoorState.Closed));
                       Assert.That(trigger, Is.EqualTo(DoorEvents.Open));
                       Assert.That(to, Is.EqualTo(DoorState.Open));
                   })
                   .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed)
                   .On((from, trigger, to, token) => {
                       Assert.That(from, Is.EqualTo(DoorState.Open));
                       Assert.That(trigger, Is.EqualTo(DoorEvents.Close));
                       Assert.That(to, Is.EqualTo(DoorState.Closed));
                   });



            Parallel.ForEach(GetTokens(machine), token => {
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, token);
                Assert.That(token.State, Is.EqualTo(DoorState.Open));
                machine.Fire(DoorEvents.Close, token);
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
            });
        }

        [Test]
        public void Guard_is_called_with_the_expected_parameters() {
            var machine = new Tsm(
                DoorState.Closed
                );

            machine.Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                   .Guard((from, trigger, to) => {
                       Assert.That(from, Is.EqualTo(DoorState.Closed));
                       Assert.That(trigger, Is.EqualTo(DoorEvents.Open));
                       Assert.That(to, Is.EqualTo(DoorState.Open));
                       return true;
                   })
                   .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed)
                   .Guard((from, trigger, to) => {
                       Assert.That(from, Is.EqualTo(DoorState.Open));
                       Assert.That(trigger, Is.EqualTo(DoorEvents.Close));
                       Assert.That(to, Is.EqualTo(DoorState.Closed));
                       return true;
                   });


            Parallel.ForEach(GetTokens(machine), token => {
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, token);
                Assert.That(token.State, Is.EqualTo(DoorState.Open));
                machine.Fire(DoorEvents.Close, token);
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
            });
        }

        [Test]
        public void Reset_method_returns_machine_to_initial_state() {
            var machine = GetFixture();

            Parallel.ForEach(GetTokens(machine), token => {
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, token);
                Assert.That(token.State, Is.EqualTo(DoorState.Open));
                machine.Reset(token);
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
            });
        }

        [Test]
        public void Reset_method_sets_machine_to_specified_state() {
            var machine = GetFixture();

            Parallel.ForEach(GetTokens(machine), token => {
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
                machine.Reset(DoorState.Open, token);
                Assert.That(token.State, Is.EqualTo(DoorState.Open));
            });
        }

        [Test]
        public void Calling_Reset_method_does_not_call_guard_or_trigger_transitions() {
            var machine = new Tsm(
                DoorState.Closed
                );

            machine.Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                   .On((t) => Assert.Fail())
                   .Guard((from, trigger, to) => false)
                   .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed)
                   .On((t) => Assert.Fail())
                   .Guard((from, trigger, to) => false);



            Parallel.ForEach(GetTokens(machine), token => {
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
                machine.Reset(DoorState.Open, token);
                Assert.That(token.State, Is.EqualTo(DoorState.Open));
                machine.Reset(DoorState.Closed, token);
                Assert.That(token.State, Is.EqualTo(DoorState.Closed));
            });
        }

        class TransitionCount : IStateToken {
            internal int transitionCount = 0;

            internal TransitionCount(TinyStateMachine<DoorState, DoorEvents, TransitionCount> stateMachine) {
                Token = stateMachine.CreateToken();
            }

            public StateToken Token { get; private set; }
        }
        [Test]
        public void OnAny_action_is_called_after_every_successful_transition() {

            var machine = GetFixture<TransitionCount>();

            machine.OnAny((count) => {
                ++count.transitionCount;
                if (count.transitionCount == 1) {
                    Assert.That(count.Token.State, Is.EqualTo(DoorState.Open));
                }
                else if (count.transitionCount == 2) {
                    Assert.That(count.Token.State, Is.EqualTo(DoorState.Closed));
                }
                else {
                    Assert.Fail();
                }
            });

            var transitionCount = new TransitionCount[ParallelTestCount];
            for (int i = 0; i < ParallelTestCount; i++) {
                transitionCount[i] = new TransitionCount(machine);
            }

            Parallel.ForEach(transitionCount, token => {
                machine.Fire(DoorEvents.Open, token);
                machine.Fire(DoorEvents.Close, token);
                Assert.That(token.transitionCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void OnAny_action_is_called_with_the_expected_parameters() {

            var machine = GetFixture<TransitionCount>();

            machine.OnAny((from, trigger, to, count) => {
                ++count.transitionCount;
                if (count.transitionCount == 1) {
                    Assert.That(from, Is.EqualTo(DoorState.Closed));
                    Assert.That(trigger, Is.EqualTo(DoorEvents.Open));
                    Assert.That(to, Is.EqualTo(DoorState.Open));
                }
                else if (count.transitionCount == 2) {
                    Assert.That(from, Is.EqualTo(DoorState.Open));
                    Assert.That(trigger, Is.EqualTo(DoorEvents.Close));
                    Assert.That(to, Is.EqualTo(DoorState.Closed));
                }
            });

            var transitionCount = new TransitionCount[ParallelTestCount];
            for (int i = 0; i < ParallelTestCount; i++) {
                transitionCount[i] = new TransitionCount(machine);
            }

            Parallel.ForEach(transitionCount, token => {
                machine.Fire(DoorEvents.Open, token);
                machine.Fire(DoorEvents.Close, token);
            });
        }
    }
}
