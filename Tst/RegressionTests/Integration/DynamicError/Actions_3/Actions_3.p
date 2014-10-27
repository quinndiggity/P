// This sample tests the deferred-by-default semantics of push statement
// and inheritance of actions by push statement; new in this sample: pop statement

event E1 assert 1;
event E2 assert 1;
event E3 assert 1;
event E4 assert 1;
event unit assert 1;

main machine Real {
    var ghost_machine: model;
    start state Real_Init {
        entry {
			ghost_machine = new Ghost(this);  
            push Real_S1;	   
        }
        on E2 do Action1;
        on unit do Action2;
		on E4 goto Real_S2;
    }

    state Real_S1 {    
		entry {
			send ghost_machine, E1;   
			raise unit;
		}
    }

    state Real_S2 {
	entry {
        //this assert is reachable:
	    assert(false);
	}
    }

	fun Action2() {
		pop;
	}
	
    fun Action1() {
        send ghost_machine, E3;
    }
 
}

model Ghost {
    var real_machine: machine;
    start state Ghost_Init {
        entry {
	      real_machine = payload as machine;
        }
        on E1 goto Ghost_S1;
    }

    state Ghost_S1 {
        entry {
			
			send real_machine, E2;
        }
        on E3 goto Ghost_S2;
    }

    state Ghost_S2 {
        entry {
			send real_machine, E4;
        }
    }
}