using System;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace M16h {

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    /// <summary>
    /// A test for TinyStateMachine using the canonical door example. Door
    /// can be in one of two states: either <see cref="DoorState.Open">Open</see>
    /// or <see cref="DoorState.Closed">closed</see>, represented by the 
    /// <see cref="DoorState"/> enum. Storage of the door can only be changed
    /// by receiving either one of two events: <see cref="DoorEvents.Open"/> 
    /// or <see cref="DoorEvents.Close"/>.
    /// </summary>
    [TestFixture]
    public class TinyStateMachineTests {
        public enum DoorState {
            Closed,
            Open,
        }

        public enum DoorEvents {
            Open,
            Close,
        }

        public class Door : TinyStateMachine<DoorState, DoorEvents>.IStorage {
            public TinyStateMachine<DoorState, DoorEvents>.Storage Memory { get; set; }
            public bool WasSlammed { get; set; }
        }

        public void WorkTheDoor() {
            // Declare the FSM and specify the starting state.
            var doorFsmCompiler = TinyStateMachine<DoorState, DoorEvents>.Create<Door>(DoorState.Closed);

            // Now configure the state transition table.
            doorFsmCompiler.Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                   .Tr<bool>(DoorState.Open, DoorEvents.Close, DoorState.Closed)
                   .On((d, slammed) => d.WasSlammed = slammed);

            var doorFsm = doorFsmCompiler.Compile();

            var door = new Door() { Memory = doorFsm.CreateMemory() };

            // As specified in the constructor, the door starts closed.
            Debug.Assert(door.Memory.IsInState(DoorState.Closed));

            // Let's trigger a transition
            doorFsm.Fire(DoorEvents.Open, door);

            // Door is now open.
            Debug.Assert(door.Memory.IsInState(DoorState.Open));

            // create as many doors as needed
            var otherDoor = new Door() { Memory = doorFsm.CreateMemory() };

            // The state machine is shared, but the state is not
            Debug.Assert(otherDoor.Memory.IsInState(DoorState.Closed));

            // According to the transition table, closing a door requires
            // a bool argument. The following will throw an exception
            bool exceptionWasThrown = false;
            try {
                // Let's trigger the other transition
                doorFsm.Fire(DoorEvents.Close, door);
            }
            catch {
                exceptionWasThrown = true;
            }
            Debug.Assert(exceptionWasThrown == true);

            // Door is still open.
            Debug.Assert(door.Memory.IsInState(DoorState.Open));

            // Slam it this time
            doorFsm.Fire(DoorEvents.Close, door, true);

            // Door is now closed.
            Debug.Assert(door.Memory.IsInState(DoorState.Closed));

            // Door is was slammed.
            Debug.Assert(door.WasSlammed);

            // still closed
            Debug.Assert(otherDoor.Memory.IsInState(DoorState.Closed));

            // According to the transition table, a closed door
            // cannot be closed. The following will throw an exception
            exceptionWasThrown = false;
            try {
                doorFsm.Fire(DoorEvents.Close, door);
            }
            catch {
                exceptionWasThrown = true;
            }
            Debug.Assert(exceptionWasThrown == true);
        }


        private static TinyStateMachine<DoorState,DoorEvents>.Machine<TinyStateMachine<DoorState, DoorEvents>.Storage> GetFixture() {
            return GetFixture<TinyStateMachine<DoorState, DoorEvents>.Storage>();
        }

        private static TinyStateMachine<DoorState,DoorEvents>.Machine<TMemory> GetFixture<TMemory>() where TMemory : TinyStateMachine<DoorState, DoorEvents>.IStorage {

            return GetFixtureCompiler<TMemory>().Compile();
        }

        private static TinyStateMachine<DoorState,DoorEvents>.Machine<TMemory>.Compiler GetFixtureCompiler<TMemory>() where TMemory : TinyStateMachine<DoorState, DoorEvents>.IStorage {
            return TinyStateMachine<DoorState,DoorEvents>.Create<TMemory>(DoorState.Closed)
                .Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed).compiler;
        }

        const int ParallelTestCount = 100;
        private static TinyStateMachine<DoorState, DoorEvents>.Storage[] GetTokens(TinyStateMachine<DoorState,DoorEvents>.Machine<TinyStateMachine<DoorState, DoorEvents>.Storage> machine) {
            var tokens = new TinyStateMachine<DoorState, DoorEvents>.Storage[ParallelTestCount];

            for (int i = 0; i < ParallelTestCount; i++) {
                tokens[i] = machine.CreateMemory();
            }
            return tokens;
        }

        [Test]
        public void State_machine_is_constructed_with_the_correct_initial_state() {
            var machine = GetFixture();

            Parallel.ForEach(GetTokens(machine), storage => {
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
            });
        }

        [Test]
        public void Test_simple_transition() {
            var machine = GetFixture();

            Parallel.ForEach(GetTokens(machine), storage => {
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Open));
            });
        }

        [Test]
        public void Test_simple_transition_2_tokens() {
            var machine = GetFixture();

            Parallel.ForEach(GetTokens(machine), storage => {
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Open));
            });
        }

        class DoorMemory : TinyStateMachine<DoorState, DoorEvents>.IStorage {
            internal bool wasDoorOpened = false;
            internal bool wasDoorClosed = false;

            internal DoorMemory(TinyStateMachine<DoorState,DoorEvents>.Machine<DoorMemory> stateMachine) {
                Memory = stateMachine.CreateMemory();
            }

            public TinyStateMachine<DoorState, DoorEvents>.Storage Memory { get; private set; }
        }

        [Test]
        public void Appropriate_action_is_called_on_transition() {

            var machine = TinyStateMachine<DoorState, DoorEvents>.Create<DoorMemory>(DoorState.Closed)
                .Tr<bool>(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                .On((t, value) => t.wasDoorOpened = value)
                .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed)
                .On((t) => t.wasDoorClosed = true)
                .Compile();

            var doorMemory = new DoorMemory[ParallelTestCount];
            for (int i = 0; i < ParallelTestCount; i++) {
                doorMemory[i] = new DoorMemory(machine);
            }

            Parallel.ForEach(doorMemory, storage => {
                Assert.That(storage.wasDoorOpened, Is.False);
                Assert.That(storage.wasDoorClosed, Is.False);

                Assert.Throws<InvalidOperationException>(() =>
                    machine.Fire(DoorEvents.Open, storage)
                );
                Assert.Throws<InvalidOperationException>(() =>
                    machine.Fire(DoorEvents.Open, storage, 14)
                );

                machine.Fire(DoorEvents.Open, storage, true);

                Assert.That(storage.wasDoorOpened, Is.True);
                Assert.That(storage.wasDoorClosed, Is.False);

                machine.Fire(DoorEvents.Close, storage);

                Assert.That(storage.wasDoorOpened, Is.True);
                Assert.That(storage.wasDoorClosed, Is.True);
            });
        }


        [Test]
        public void Firing_trigger_with_no_valid_transition_throws_exception() {
            var machine = GetFixture();
            var storage = machine.CreateMemory();
            Assert.Throws<InvalidOperationException>(
                () => machine.Fire(DoorEvents.Close, storage)
                );
        }

        [Test]
        public void Guard_can_stop_transition() {
            var machine = TinyStateMachine<DoorState,DoorEvents>.Create(DoorState.Closed)
                .Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                .Guard(() => false)
                .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed)
                .Compile();

            Parallel.ForEach(GetTokens(machine), storage => {
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
            });
        }

        [Test]
        public void Action_is_called_with_the_expected_parameters() {
            var machine = TinyStateMachine<DoorState, DoorEvents>.Create(DoorState.Closed)

            .Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
            .On((from, trigger, to, storage) => {
                Assert.That(from, Is.EqualTo(DoorState.Closed));
                Assert.That(trigger, Is.EqualTo(DoorEvents.Open));
                Assert.That(to, Is.EqualTo(DoorState.Open));
            })
            .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed)
            .On((from, trigger, to, storage) => {
                Assert.That(from, Is.EqualTo(DoorState.Open));
                Assert.That(trigger, Is.EqualTo(DoorEvents.Close));
                Assert.That(to, Is.EqualTo(DoorState.Closed));
            })
            .Compile();



            Parallel.ForEach(GetTokens(machine), storage => {
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Open));
                machine.Fire(DoorEvents.Close, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
            });
        }

        [Test]
        public void Guard_is_called_with_the_expected_parameters() {
            var machine = TinyStateMachine<DoorState, DoorEvents>.Create(DoorState.Closed)
                .Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
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
                })
                .Compile();


            Parallel.ForEach(GetTokens(machine), storage => {
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Open));
                machine.Fire(DoorEvents.Close, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
            });
        }

        [Test]
        public void Reset_method_returns_machine_to_initial_state() {
            var machine = GetFixture();

            Parallel.ForEach(GetTokens(machine), storage => {
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Open));
                machine.Reset(storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
            });
        }

        [Test]
        public void Reset_method_sets_machine_to_specified_state() {
            var machine = GetFixture();

            Parallel.ForEach(GetTokens(machine), storage => {
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
                machine.Reset(DoorState.Open, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Open));
            });
        }

        [Test]
        public void Calling_Reset_method_does_not_call_guard_or_trigger_transitions() {
            var machine = TinyStateMachine<DoorState, DoorEvents>.Create(DoorState.Closed)
                .Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                .On((t) => Assert.Fail())
                .Guard((from, trigger, to) => false)
                .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed)
                .On((t) => Assert.Fail())
                .Guard((from, trigger, to) => false)
                .Compile();



            Parallel.ForEach(GetTokens(machine), storage => {
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
                machine.Reset(DoorState.Open, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Open));
                machine.Reset(DoorState.Closed, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
            });
        }

        class TransitionCount : TinyStateMachine<DoorState, DoorEvents>.IStorage {
            internal int transitionCount = 0;

            internal TransitionCount(TinyStateMachine<DoorState,DoorEvents>.Machine<TransitionCount> stateMachine) {
                Memory = stateMachine.CreateMemory();
            }

            public TinyStateMachine<DoorState, DoorEvents>.Storage Memory { get; private set; }
        }
        [Test]
        public void OnAny_action_is_called_after_every_successful_transition() {

            var machine = GetFixtureCompiler<TransitionCount>()
                .OnAny((count) => {
                    ++count.transitionCount;
                    if (count.transitionCount == 1) {
                        Assert.That(count.Memory.State, Is.EqualTo(DoorState.Open));
                    }
                    else if (count.transitionCount == 2) {
                        Assert.That(count.Memory.State, Is.EqualTo(DoorState.Closed));
                    }
                    else {
                        Assert.Fail();
                    }
                })
            .Compile();

            var transitionCount = new TransitionCount[ParallelTestCount];
            for (int i = 0; i < ParallelTestCount; i++) {
                transitionCount[i] = new TransitionCount(machine);
            }

            Parallel.ForEach(transitionCount, storage => {
                machine.Fire(DoorEvents.Open, storage);
                machine.Fire(DoorEvents.Close, storage);
                Assert.That(storage.transitionCount, Is.EqualTo(2));
            });
        }

        [Test]
        public void OnAny_action_is_called_with_the_expected_parameters() {

            var machine = GetFixtureCompiler<TransitionCount>()
                .OnAny((from, trigger, to, count) => {
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
                })
                .Compile();

            var transitionCount = new TransitionCount[ParallelTestCount];
            for (int i = 0; i < ParallelTestCount; i++) {
                transitionCount[i] = new TransitionCount(machine);
            }

            Parallel.ForEach(transitionCount, storage => {
                machine.Fire(DoorEvents.Open, storage);
                machine.Fire(DoorEvents.Close, storage);
            });
        }
    }
}
