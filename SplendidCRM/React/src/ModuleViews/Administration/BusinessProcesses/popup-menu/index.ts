import ReplaceMenuProvider from './ReplaceMenuProvider';
import popupMenuModule from 'diagram-js/lib/features/popup-menu';
import replaceModule from '../replace';

let def: any =
{
	__depends__:
	[
		popupMenuModule,
		replaceModule
	],
	__init__: [ 'replaceMenuProvider' ],
	replaceMenuProvider: [ 'type', ReplaceMenuProvider ]
};

export default def;
