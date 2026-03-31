import BpmnReplace from './BpmnReplace';
import replaceModule from 'diagram-js/lib/features/replace';
import selectionModule from 'diagram-js/lib/features/selection';

let def: any =
{
	__depends__:
	[
		replaceModule,
		selectionModule
	],
	bpmnReplace: [ 'type', BpmnReplace ]
};

export default def;
