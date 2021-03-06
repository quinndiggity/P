﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;


namespace P.Runtime
{
    public abstract class PrtMachine
    {
        #region Fields
        public int instanceNumber;
        public List<PrtValue> fields;
        protected PrtValue eventValue;
        protected PrtStateStack stateStack;
        protected PrtFunStack invertedFunStack;
        protected PrtContinuation continuation;
        public PrtMachineStatus currentStatus;
        protected PrtNextStatemachineOperation nextSMOperation;
        protected PrtStateExitReason stateExitReason;
        public PrtValue currentTrigger;
        public PrtValue currentPayload;
        public PrtState destOfGoto;
        //just a reference to stateImpl
        protected StateImpl stateImpl;
        #endregion

        #region Constructor
        public PrtMachine()
        {
            this.instanceNumber = 0;
            this.fields = new List<PrtValue>();
            this.eventValue = PrtValue.NullValue;
            this.stateStack = new PrtStateStack();
            this.invertedFunStack = new PrtFunStack();
            this.continuation = new PrtContinuation();
            this.currentTrigger = PrtValue.NullValue;
            this.currentPayload = PrtValue.NullValue;
            this.currentStatus = PrtMachineStatus.Enabled;
            this.nextSMOperation = PrtNextStatemachineOperation.EntryOperation;
            this.stateExitReason = PrtStateExitReason.NotExit;
            
            this.stateImpl = null;
        }
        #endregion


        public abstract string Name
        {
            get;
        }

        public abstract PrtState StartState
        {
            get;
        }

        public abstract void PrtEnqueueEvent(PrtValue e, PrtValue arg, PrtMachine source);

        public PrtState CurrentState
        {
            get
            {
                return stateStack.TopOfStack.state;
            }
        }

        #region Prt Helper functions
        public PrtFun PrtFindActionHandler(PrtValue ev)
        {
            var tempStateStack = stateStack.Clone();
            while (tempStateStack.TopOfStack != null)
            {
                if (tempStateStack.TopOfStack.state.dos.ContainsKey(ev))
                {
                    return tempStateStack.TopOfStack.state.dos[ev];
                }
                else
                    tempStateStack.PopStackFrame();
            }
            Debug.Assert(false);
            return null;
        }

        public void PrtPushState(PrtState s)
        {
            stateStack.PushStackFrame(s);
        }

        public bool PrtPopState(bool isPopStatement)
        {
            Debug.Assert(stateStack.TopOfStack != null);
            //pop stack
            stateStack.PopStackFrame();

            if (stateStack.TopOfStack == null)
            {
                if (isPopStatement)
                {
                    throw new PrtInvalidPopStatement();
                }
                //TODO : Handle the monitor machine case separately for the halt event
                else if (eventValue.IsEqual(PrtValue.HaltEvent))
                {
                    throw new PrtUnhandledEventException();
                }
                else
                {
                    currentStatus = PrtMachineStatus.Halted;
                }
            }

            return currentStatus == PrtMachineStatus.Halted;

        }

        public void PrtChangeState(PrtState s)
        {
            Debug.Assert(stateStack.TopOfStack != null);
            stateStack.PopStackFrame();
            stateStack.PushStackFrame(s);
        }

        public PrtFunStackFrame PrtPopFunStackFrame()
        {
            return invertedFunStack.PopFun();
        }

        public void PrtPushFunStackFrame(PrtFun fun, List<PrtValue> local)
        {
            invertedFunStack.PushFun(fun, local);
        }

        public void PrtPushFunStackFrame(PrtFun fun, List<PrtValue> local, int retTo)
        {
            invertedFunStack.PushFun(fun, local, retTo);
        }

        public void PrtExecuteExitFunction()
        {
            PrtPushFunStackFrame(CurrentState.exitFun, CurrentState.exitFun.CreateLocals());
            invertedFunStack.TopOfStack.fun.Execute(stateImpl, this);
        }

        public bool PrtIsTransitionPresent(PrtValue ev)
        {
            return CurrentState.transitions.ContainsKey(ev);
        }

        public bool PrtIsActionInstalled(PrtValue ev)
        {
            return CurrentState.dos.ContainsKey(ev);
        }

        public void PrtExecuteTransitionFun(PrtValue ev)
        {
            // Shaz: Figure out how to handle the transfer stuff for payload !!!
            PrtPushFunStackFrame(CurrentState.transitions[ev].transitionFun, CurrentState.transitions[ev].transitionFun.CreateLocals(currentPayload));
            invertedFunStack.TopOfStack.fun.Execute(stateImpl, this);
        }

        public void PrtFunContReturn(List<PrtValue> retLocals)
        {
            continuation.reason = PrtContinuationReason.Return;
            continuation.retVal = null;
            continuation.retLocals = retLocals;
        }


        public void PrtFunContReturnVal(PrtValue val, List<PrtValue> retLocals)
        {
            continuation.reason = PrtContinuationReason.Return;
            continuation.retVal = val;
            continuation.retLocals = retLocals;
        }

        public void PrtFunContPop()
        {
            continuation.reason = PrtContinuationReason.Pop;
        }

        public void PrtFunContRaise()
        {
            continuation.reason = PrtContinuationReason.Raise;
        }

        public void PrtFunContSend(PrtFun fun, List<PrtValue> locals, int ret)
        {
            PrtPushFunStackFrame(fun, locals, ret);
            continuation.reason = PrtContinuationReason.Send;
        }

        void PrtFunContNewMachine(PrtFun fun, List<PrtValue> locals, PrtImplMachine o, int ret)
        {
            PrtPushFunStackFrame(fun, locals, ret);
            continuation.reason = PrtContinuationReason.NewMachine;
            continuation.createdMachine = o;
        }

        void PrtFunContReceive(PrtFun fun, List<PrtValue> locals, int ret)
        {
            PrtPushFunStackFrame(fun, locals, ret);
            continuation.reason = PrtContinuationReason.Receive;

        }

        void PrtFunContNondet(PrtFun fun, List<PrtValue> locals, int ret)
        {
            PrtPushFunStackFrame(fun, locals, ret);
            continuation.reason = PrtContinuationReason.Nondet;
        }
        #endregion
    }
    public class PrtCommonFunctions
    {
        public static PrtIgnoreFun IgnoreFun = new PrtIgnoreFun();
        public static PrtSkipFun SkipFun = new PrtSkipFun();
    }

    public class PrtIgnoreFun : PrtFun
    {
        public override bool IsAnonFun
        {
            get
            {
                return true;
            }
        }

        public override void Execute(StateImpl application, PrtMachine parent)
        {
            throw new NotImplementedException();
        }

        public override List<PrtValue> CreateLocals(params PrtValue[] args)
        {
            throw new NotImplementedException();
        }
    }

    public class PrtSkipFun : PrtFun
    {
        public override bool IsAnonFun
        {
            get
            {
                return true;
            }
        }

        public override void Execute(StateImpl application, PrtMachine parent)
        {
            throw new NotImplementedException();
        }

        public override List<PrtValue> CreateLocals(params PrtValue[] args)
        {
            throw new NotImplementedException();
        }
    }

    public abstract class PrtFun
    {
        public abstract bool IsAnonFun
        {
            get;
        } 

        public List<Dictionary<PrtValue, PrtFun>> receiveCases;
        
        public PrtFun()
        {
            receiveCases = new List<Dictionary<PrtValue, PrtFun>>();
        }

        public abstract List<PrtValue> CreateLocals(params PrtValue[] args);

        public abstract void Execute(StateImpl application, PrtMachine parent);
    }

    public class PrtEvent
    {
        public static int DefaultMaxInstances = int.MaxValue;
        

        public string name;
        public PrtType payloadType;
        public int maxInstances;
        public bool doAssume;

        public PrtEvent(string name, PrtType payload, int mInstances, bool doAssume)
        {
            this.name = name;
            this.payloadType = payload;
            this.maxInstances = mInstances;
            this.doAssume = doAssume;
        }
    };

    public class PrtTransition
    {
        public PrtFun transitionFun; // isPush <==> fun == null
        public PrtState gotoState;
        public bool isPushTran;
        public PrtTransition(PrtFun fun, PrtState toState, bool isPush)
        {
            this.transitionFun = fun;
            this.gotoState = toState;
            this.isPushTran = isPush;

        }
    };

    public enum StateTemperature
    {
        Cold,
        Warm,
        Hot
    };

    public class PrtState
    {
        public string name;
        public PrtFun entryFun;
        public PrtFun exitFun;
        public Dictionary<PrtValue, PrtTransition> transitions;
        public Dictionary<PrtValue, PrtFun> dos;
        public bool hasNullTransition;
        public StateTemperature temperature;
        public HashSet<PrtEventValue> deferredSet;

        public PrtState(string name, PrtFun entryFun, PrtFun exitFun, bool hasNullTransition, StateTemperature temperature)
        {
            this.name = name;
            this.entryFun = entryFun;
            this.exitFun = exitFun;
            this.transitions = new Dictionary<PrtValue, PrtTransition>();
            this.dos = new Dictionary<PrtValue, PrtFun>();
            this.hasNullTransition = hasNullTransition;
            this.temperature = temperature;
        }
    };

    internal class PrtEventNode
    {
        public PrtValue ev;
        public PrtValue arg;

        public PrtEventNode(PrtValue e, PrtValue payload)
        {
            ev = e;
            arg = payload.Clone();
        }

        public PrtEventNode Clone()
        {
            return new PrtEventNode(this.ev, this.arg);
        }
    }

    public class PrtEventBuffer
    {
        List<PrtEventNode> events;
        public PrtEventBuffer()
        {
            events = new List<PrtEventNode>();
        }

        public PrtEventBuffer Clone()
        {
            var clonedVal = new PrtEventBuffer();
            foreach(var ev in this.events)
            {
                clonedVal.events.Add(ev.Clone());
            }
            return clonedVal;
        }
        public int Size()
        {
            return events.Count();
        }
        public int CalculateInstances(PrtValue e)
        {
            return events.Select(en => en.ev).Where(ev => ev == e).Count();
        }

        public void EnqueueEvent(PrtValue e, PrtValue arg)
        {
            Debug.Assert(e is PrtEventValue, "Illegal enqueue of null event");
            PrtEventValue ev = e as PrtEventValue;
            if (ev.evt.maxInstances == PrtEvent.DefaultMaxInstances)
            {
                events.Add(new PrtEventNode(e, arg));
            }
            else
            {
                if (CalculateInstances(e) == ev.evt.maxInstances)
                {
                    if (ev.evt.doAssume)
                    {
                        throw new PrtAssumeFailureException();
                    }
                    else
                    {
                        throw new PrtMaxEventInstancesExceededException(
                            String.Format(@"< Exception > Attempting to enqueue event {0} more than max instance of {1}\n", ev.evt.name, ev.evt.maxInstances));
                    }
                }
                else
                {
                    events.Add(new PrtEventNode(e, arg));
                }
            }
        }

        public bool DequeueEvent(PrtImplMachine owner)
        {
            HashSet<PrtEventValue> deferredSet;
            HashSet<PrtValue> receiveSet;

            deferredSet = owner.CurrentState.deferredSet;
            receiveSet = owner.receiveSet;

            int iter = 0;
            while (iter < events.Count)
            { 
                if ((receiveSet.Count == 0 && !deferredSet.Contains(events[iter].ev))
                    || (receiveSet.Count > 0 && receiveSet.Contains(events[iter].ev)))
                {
                    owner.currentTrigger = events[iter].ev;
                    owner.currentPayload = events[iter].arg;
                    events.Remove(events[iter]);
                    return true;
                }
                else
                {
                    continue;
                }
            }

            return false;
        }

        public bool IsEnabled(PrtImplMachine owner)
        {
            HashSet<PrtEventValue> deferredSet;
            HashSet<PrtValue> receiveSet;

            deferredSet = owner.CurrentState.deferredSet;
            receiveSet = owner.receiveSet;
            foreach (var evNode in events)
            {
                if ((receiveSet.Count == 0 && !deferredSet.Contains(evNode.ev))
                    || (receiveSet.Count > 0 && receiveSet.Contains(evNode.ev)))
                {
                    return true;
                }

            }
            return false;
        }
    }

    public class PrtStateStackFrame
    {
        public PrtState state;
        //event value because you cannot defer null value
        public HashSet<PrtValue> deferredSet;
        //action set can have null value
        public HashSet<PrtValue> actionSet;

        public PrtStateStackFrame(PrtState st, HashSet<PrtValue> defSet, HashSet<PrtValue> actSet)
        {
            this.state = st;
            this.deferredSet = new HashSet<PrtValue>();
            foreach (var item in defSet)
                this.deferredSet.Add(item);

            this.actionSet = new HashSet<PrtValue>();
            foreach (var item in actSet)
                this.actionSet.Add(item);
        }

        public PrtStateStackFrame Clone()
        {
            return new PrtStateStackFrame(state, deferredSet, actionSet);
        }

    }
    
    public class PrtStateStack
    {
        public PrtStateStack()
        {
            stateStack = new Stack<PrtStateStackFrame>();
        }

        private Stack<PrtStateStackFrame> stateStack;

        public PrtStateStackFrame TopOfStack
        {
            get
            {
                if (stateStack.Count > 0)
                    return stateStack.Peek();
                else
                    return null;
            }
        }
       
        public PrtStateStack Clone()
        {
            var clone = new PrtStateStack();
            foreach(var s in stateStack)
            {
                clone.stateStack.Push(s.Clone());
            }
            clone.stateStack.Reverse();
            return clone;
        }

        public void PopStackFrame()
        {
            stateStack.Pop();
        }


        public void PushStackFrame(PrtState state)
        {
            var deferredSet = new HashSet<PrtValue>();
            if (TopOfStack != null)
            {
                deferredSet.UnionWith(TopOfStack.deferredSet);
            }
            deferredSet.UnionWith(state.deferredSet);
            deferredSet.ExceptWith(state.dos.Keys);
            deferredSet.ExceptWith(state.transitions.Keys);

            var actionSet = new HashSet<PrtValue>();
            if (TopOfStack != null)
            {
                actionSet.UnionWith(TopOfStack.actionSet);
            }
            actionSet.ExceptWith(state.deferredSet);
            actionSet.UnionWith(state.dos.Keys);
            actionSet.ExceptWith(state.transitions.Keys);

            //push the new state on stack
            stateStack.Push(new PrtStateStackFrame(state, deferredSet, actionSet));
        }

        public bool HasNullTransitionOrAction()
        {
            if (TopOfStack.state.hasNullTransition) return true;
            return TopOfStack.actionSet.Contains(PrtValue.NullValue);
        }
    }

    public enum PrtContinuationReason : int
    {
        Return,
        Nondet,
        Pop,
        Raise,
        Receive,
        Send,
        NewMachine,
        Goto
    };

    public class PrtFunStackFrame
    {
        public int returnTolocation;
        public List<PrtValue> locals;
        
        public PrtFun fun;
        public PrtFunStackFrame(PrtFun fun,  List<PrtValue> locs)
        {
            this.fun = fun;
            this.locals = new List<PrtValue>();
            foreach(var l in locs)
            {
                locals.Add(l.Clone());
            }
            returnTolocation = 0;
        }

        public PrtFunStackFrame(PrtFun fun, List<PrtValue> locs, int retLocation)
        {
            this.fun = fun;
            this.locals = new List<PrtValue>();
            foreach (var l in locs)
            {
                locals.Add(l.Clone());
            }
            returnTolocation = retLocation;
        }

        public PrtFunStackFrame Clone()
        {
            return new PrtFunStackFrame(this.fun, this.locals, this.returnTolocation);
        }
    }

    public class PrtFunStack
    {
        private Stack<PrtFunStackFrame> funStack;
        public PrtFunStack()
        {
            funStack = new Stack<PrtFunStackFrame>();
        }

        public PrtFunStack Clone()
        {
            var clonedStack = new PrtFunStack();
            foreach(var frame in funStack)
            {
                clonedStack.funStack.Push(frame.Clone());
            }
            clonedStack.funStack.Reverse();
            return clonedStack;
        }

        public PrtFunStackFrame TopOfStack
        {
            get
            {
                if (funStack.Count == 0)
                    return null;
                else
                    return funStack.Peek();
            }
        }

        public void PushFun(PrtFun fun, List<PrtValue> locals)
        {
            funStack.Push(new PrtFunStackFrame(fun, locals));
        }

        public void PushFun(PrtFun fun, List<PrtValue> locals, int retLoc)
        {
            funStack.Push(new PrtFunStackFrame(fun, locals, retLoc));
        }

        public PrtFunStackFrame PopFun()
        {
            return funStack.Pop();
        }

        

    }

    public class PrtContinuation
    {
        
        public PrtContinuationReason reason;
        public PrtImplMachine createdMachine;
        public int receiveIndex;
        public PrtValue retVal;
        public List<PrtValue> retLocals;
        // The nondet field is different from the fields above because it is used 
        // by ReentrancyHelper to pass the choice to the nondet choice point.
        // Therefore, nondet should not be reinitialized in this class.
        public bool nondet;

        public PrtContinuation()
        {
            reason = PrtContinuationReason.Return;
            createdMachine = null;
            retVal = null;
            nondet = false;
            retLocals = new List<PrtValue>();
            receiveIndex = -1;
        }

        public PrtContinuation Clone()
        {
            var clonedVal = new PrtContinuation();
            clonedVal.reason = this.reason;
            clonedVal.createdMachine = this.createdMachine;
            clonedVal.receiveIndex = this.receiveIndex;
            clonedVal.retVal = this.retVal.Clone();
            foreach(var loc in retLocals)
            {
                clonedVal.retLocals.Add(loc.Clone());
            }

            return clonedVal;
        }
    }
}