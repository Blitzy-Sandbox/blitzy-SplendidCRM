import PaletteProvider from './PaletteProvider';
import paletteModule from 'diagram-js/lib/features/palette';
import createModule from 'diagram-js/lib/features/create';
import spaceToolModule from 'diagram-js/lib/features/space-tool';
import lassoToolModule from 'diagram-js/lib/features/lasso-tool';
import handToolModule from 'diagram-js/lib/features/hand-tool';
import translateModule from 'diagram-js/lib/i18n/translate';
import globalConnectModule from 'bpmn-js/lib/features/global-connect';

let def: any =
{
	__depends__: 
	[
		paletteModule,
		createModule,
		spaceToolModule,
		lassoToolModule,
		handToolModule,
		translateModule,
		// 03/02/2022 Paul.  bpmn-js no longer includes global-connect after version 1.3.3.  Use version in diagram-js. 
		globalConnectModule
	],
	__init__: ['paletteProvider'],
	paletteProvider: ['type', PaletteProvider]
};

export default def;
