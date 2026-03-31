import BpmnRules from './BpmnRules';
import rulesModule from 'diagram-js/lib/features/rules';

let def: any =
{
	__depends__:
	[
		rulesModule
	],
	__init__: [ 'bpmnRules' ],
	bpmnRules: [ 'type', BpmnRules ]
};

export default def;
