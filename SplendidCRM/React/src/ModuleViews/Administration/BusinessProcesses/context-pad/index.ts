import ContextPadProvider from './ContextPadProvider';
import diagramJsDirectEditing from 'diagram-js-direct-editing';
import contextPadModule from 'diagram-js/lib/features/context-pad';
import selectionModule from 'diagram-js/lib/features/selection';
import connectModule from 'diagram-js/lib/features/connect';
import createModule from 'diagram-js/lib/features/create';
import popupMenuModule from '../popup-menu';

let def: any =
{
	__depends__:
	[
		diagramJsDirectEditing,
		contextPadModule,
		selectionModule,
		connectModule,
		createModule,
		popupMenuModule
	],
	__init__: [ 'contextPadProvider' ],
	contextPadProvider: [ 'type', ContextPadProvider ]
};

export default def;
