﻿using System;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

using Storage = M16h.TinyStateMachine<M16h.TinyStateMachineTests.DoorState, M16h.TinyStateMachineTests.DoorEvents>.Storage;
using IStorage = M16h.TinyStateMachine<M16h.TinyStateMachineTests.DoorState, M16h.TinyStateMachineTests.DoorEvents>.IStorage;
using Tsm = M16h.TinyStateMachine<M16h.TinyStateMachineTests.DoorState, M16h.TinyStateMachineTests.DoorEvents>;
using DefaultMachine = M16h.TinyStateMachine<M16h.TinyStateMachineTests.DoorState, M16h.TinyStateMachineTests.DoorEvents>.Machine<M16h.TinyStateMachine<M16h.TinyStateMachineTests.DoorState, M16h.TinyStateMachineTests.DoorEvents>.Storage>;
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
        internal enum DoorState {
            Closed,
            Open,
        }

        internal enum DoorEvents {
            Open,
            Close,
        }


        private static Tsm.Machine<Storage> GetFixture() {
            return GetFixture<Storage>();
        }

        private static Tsm.Machine<TMemory> GetFixture<TMemory>() where TMemory : IStorage {
            var machine = new Tsm.Machine<TMemory>(
                DoorState.Closed
                );
            machine.Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                   .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed);
            return machine;
        }

        const int ParallelTestCount = 100;
        private static Storage[] GetTokens(Tsm.Machine<Storage> machine) {
            var tokens = new Storage[ParallelTestCount];

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

        class DoorMemory : IStorage {
            internal bool wasDoorOpened = false;
            internal bool wasDoorClosed = false;

            internal DoorMemory(Tsm.Machine<DoorMemory> stateMachine) {
                Memory = stateMachine.CreateMemory();
            }

            public Storage Memory { get; private set; }
        }

        [Test]
        public void Appropriate_action_is_called_on_transition() {

            var machine = new Tsm.Machine<DoorMemory>(
                DoorState.Closed
                );

            machine.Tr<bool>(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                   .On<bool>((t,value) => t.wasDoorOpened = value)
                   .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed)
                   .On((t) => t.wasDoorClosed = true);

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
            var machine = new Tsm.Machine<Storage>(
                DoorState.Closed
                );
            machine.Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                   .Guard(() => false)
                   .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed);

            Parallel.ForEach(GetTokens(machine), storage => {
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
                machine.Fire(DoorEvents.Open, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
            });
        }

        [Test]
        public void Action_is_called_with_the_expected_parameters() {
            var machine = new DefaultMachine(
                DoorState.Closed
                );
           machine.Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
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
                  });



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
            var machine = new DefaultMachine(
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
            var machine = new DefaultMachine(
                DoorState.Closed
                );

            machine.Tr(DoorState.Closed, DoorEvents.Open, DoorState.Open)
                   .On((t) => Assert.Fail())
                   .Guard((from, trigger, to) => false)
                   .Tr(DoorState.Open, DoorEvents.Close, DoorState.Closed)
                   .On((t) => Assert.Fail())
                   .Guard((from, trigger, to) => false);



            Parallel.ForEach(GetTokens(machine), storage => {
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
                machine.Reset(DoorState.Open, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Open));
                machine.Reset(DoorState.Closed, storage);
                Assert.That(storage.State, Is.EqualTo(DoorState.Closed));
            });
        }

        class TransitionCount : IStorage {
            internal int transitionCount = 0;

            internal TransitionCount(Tsm.Machine<TransitionCount> stateMachine) {
                Memory = stateMachine.CreateMemory();
            }

            public Storage Memory { get; private set; }
        }
        [Test]
        public void OnAny_action_is_called_after_every_successful_transition() {

            var machine = GetFixture<TransitionCount>();

            machine.OnAny((count) => {
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
            });

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

            Parallel.ForEach(transitionCount, storage => {
                machine.Fire(DoorEvents.Open, storage);
                machine.Fire(DoorEvents.Close, storage);
            });
        }
    }
}
