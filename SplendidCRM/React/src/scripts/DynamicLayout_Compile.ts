/*
 * This program is free software: you can redistribute it and/or modify it under the terms of the 
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3 
 * of the License, or (at your option) any later version.
 * 
 * In accordance with Section 7(b) of the GNU Affero General Public License version 3, 
 * the Appropriate Legal Notices must display the following words on all interactive user interfaces: 
 * "Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved."
 */

// 1. React and third-party libraries.
import React from 'react';
import { Modal } from 'react-bootstrap';
// 09/19/2024 Paul.  To use XMLParser in dynamic control, must use root. 
import XMLParser from 'fast-xml-parser';
import qs from 'query-string';
import Babel from '@babel/standalone';
import { SplendidHistory, withRouter, Link, Route, Navigate } from '../Router5';
import type { RouteComponentProps } from '../Router5';
import * as am4core from '@amcharts/amcharts4/core';
import * as am4charts from '@amcharts/amcharts4/charts';
import { observer } from 'mobx-react';
import * as mobx from 'mobx';
import BootstrapTable from 'react-bootstrap-table-next';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import moment from 'moment';
import { RouterStore } from 'mobx-react-router';

// Stubs for removed packages — provide no-op implementations for backward compatibility
// with @babel/standalone compiled TSX that may still reference these modules.
// react-pose: deprecated animation library replaced in the codebase migration.
const posed: any = new Proxy({}, {
	get: (_: any, tag: string) => (_config: any) => {
		return (props: any) => React.createElement(tag as any, null, props?.children);
	}
});
// react-lifecycle-appear: unmaintained lifecycle hook library replaced in migration.
const Appear: any = (props: any) => props?.children || null;

// 2. Types — value imports (classes that exist at runtime)
import ACL_ACCESS from '../types/ACL_ACCESS';
import ACL_FIELD_ACCESS from '../types/ACL_FIELD_ACCESS';
import OrdersLineItemsEditor from '../types/OrdersLineItemsEditor';
import { EditComponent } from '../types/EditComponent';
import { DetailComponent } from '../types/DetailComponent';
import { HeaderButtons } from '../types/HeaderButtons';
// 2b. Types — type-only imports (interfaces erased at runtime)
import type DYNAMIC_BUTTON from '../types/DYNAMIC_BUTTON';
import type DETAILVIEWS_RELATIONSHIP from '../types/DETAILVIEWS_RELATIONSHIP';
import type MODULE from '../types/MODULE';
import type RELATIONSHIPS from '../types/RELATIONSHIPS';
import type SINGLE_SIGN_ON from '../types/SINGLE_SIGN_ON';
import type DASHBOARDS_PANELS from '../types/DASHBOARDS_PANELS';
import type SHORTCUT from '../types/SHORTCUT';
import type TAB_MENU from '../types/TAB_MENU';
import type IDashletProps from '../types/IDashletProps';
import type IOrdersLineItemsEditorProps from '../types/IOrdersLineItemsEditorProps';
import type IOrdersLineItemsEditorState from '../types/IOrdersLineItemsEditorState';
import type { IEditComponentProps } from '../types/EditComponent';
import type { IDetailViewProps, IDetailComponentProps, IDetailComponentState } from '../types/DetailComponent';

// 3. Scripts
import Sql from '../scripts/Sql';
import L10n from '../scripts/L10n';
// 10/16/2021 Paul.  Add support for user currency. 
import C10n from '../scripts/C10n';
import Security from '../scripts/Security';
import { Crm_Config, Crm_Modules, Crm_Teams, Crm_Users } from '../scripts/Crm';
import { FromJsonDate, ToJsonDate, formatDate, formatCurrency, formatNumber } from '../scripts/Formatting';
import Credentials from '../scripts/Credentials';
import SplendidCache from '../scripts/SplendidCache';
import SplendidDynamic from '../scripts/SplendidDynamic';
import SplendidDynamic_DetailView from '../scripts/SplendidDynamic_DetailView';
import { sPLATFORM_LAYOUT } from '../scripts/SplendidInitUI';
// 11/28/2021 Paul.  UpdateRelatedList is needed to allow customize of popups. 
import { UpdateModule, DeleteModuleItem, DeleteModuleRecurrences, MassDeleteModule, MassUpdateModule, MassSync, MassUnsync, ArchiveMoveData, ArchiveRecoverData, UpdateSavedSearch, DeleteRelatedItem, UpdateRelatedItem, UpdateRelatedList, AdminProcedure, ExecProcedure } from '../scripts/ModuleUpdate';
import { CreateSplendidRequest, GetSplendidResult } from '../scripts/SplendidRequest';
import { DetailView_LoadItem, DetailView_LoadLayout, DetailView_LoadPersonalInfo, DetailView_RemoveField, DetailView_HideField, DetailView_FindField, DetailView_GetTabList, DetailView_ActivateTab } from '../scripts/DetailView';
// 11/25/2020 Paul.  EditView_UpdateREPEAT_TYPE is used in Calls/Meetings EditView. 
import { EditView_LoadItem, EditView_LoadLayout, EditView_ConvertItem, EditView_RemoveField, EditView_InitItem, EditView_FindField, EditView_HideField, EditView_UpdateREPEAT_TYPE, EditView_GetTabList, EditView_ActivateTab } from '../scripts/EditView';
import { Application_GetReactLoginState, jsonReactState, Application_GetReactState, Admin_GetReactState, Application_ClearStore, Application_UpdateStoreLastDate } from '../scripts/Application';
import { AppName, AppVersion } from '../AppVersion';
import { AuthenticatedMethod, IsAuthenticated, LoginRedirect, GetUserProfile, GetMyUserProfile, GetUserID, Login, ForgotPassword } from '../scripts/Login';
import { Right, Left, StartsWith, EndsWith, Trim, uuidFast, isEmptyObject, isTouchDevice, base64ArrayBuffer, isMobileDevice, screenWidth, screenHeight } from '../scripts/utility';
import { NormalizeDescription, XssFilter } from '../scripts/EmailUtils';
import { ListView_LoadTable, ListView_LoadModule, ListView_LoadLayout, ListView_LoadModulePaginated, ListView_LoadTablePaginated, ListView_LoadTableWithAggregate } from '../scripts/ListView';
import { ConvertEditViewFieldToDetailViewField } from '../scripts/ConvertLayoutField';
import { GetInviteesActivities } from '../scripts/CalendarView';
import { DynamicButtons_LoadLayout } from '../scripts/DynamicButtons';

// 4. Components and Views. 
import DumpSQL from '../components/DumpSQL';
import SearchView from '../views/SearchView';
// 08/11/2021 Paul.  Allow custom popups from custom layouts. 
import DynamicPopupView from '../views/DynamicPopupView';
import EditView from '../views/EditView';
import ListView from '../views/ListView';
import DetailView from '../views/DetailView';
import DynamicButtons from '../components/DynamicButtons';
import ModuleHeader from '../components/ModuleHeader';
import ProcessButtons from '../components/ProcessButtons';
import ErrorComponent from '../components/ErrorComponent';
import Collapsable from '../components/Collapsable';
import DetailViewLineItems from '../views/DetailViewLineItems';
import DetailViewRelationships from '../views/DetailViewRelationships';
import SplendidGrid from '../components/SplendidGrid';
import SchedulingGrid from '../components/SchedulingGrid';
import ListHeader from '../components/ListHeader';
import SearchTabs from '../components/SearchTabs';
import ExportHeader from '../components/ExportHeader';
import PreviewDashboard from '../views/PreviewDashboard';
import MassUpdate from '../views/MassUpdate';
import DynamicMassUpdate from '../views/DynamicMassUpdate';
// 08/30/2022 Paul.  A customer needs to have DynamicDetailView for a custom DetailView. 
import DynamicDetailView from '../views/DynamicDetailView';
// 07/10/2019 Paul.  Cannot use DynamicEditView as it causes any file that includes SearchView, PopupView to fail to load in DynamicLayout, including SplendidDynamic_EditView. 
// 02/03/2024 Paul.  DynamicEditView seems to be cause SplendidDynamic_EditView to fail again. 
import DynamicEditView from '../views/DynamicEditView';
import DynamicListView from '../views/DynamicListView';
import AuditView from '../views/AuditView';
import HeaderButtonsFactory from '../ThemeComponents/HeaderButtonsFactory';
import SubPanelButtonsFactory from '../ThemeComponents/SubPanelButtonsFactory';
import EditViewLineItems from '../views/EditViewLineItems';
// 02/10/2022 Paul.  AcocuntsDetailViewJS uses ActivitiesPopupView and PersonalInfoView, so we must export these views. 
import ActivitiesPopupView from '../views/ActivitiesPopupView';
import PersonalInfoView from '../views/PersonalInfoView';
// 04/13/2022 Paul.  Add LayoutTabs to Pacific theme. 
import LayoutTabs from '../components/LayoutTabs';
// 10/01/2022 Paul.  Base dashlets should have been added long ago.  They are needed when making custom from base. 
import BaseMyDashlet from '../Dashlets/BaseMyDashlet';
import BaseMyFavoriteDashlet from '../Dashlets/BaseMyFavoriteDashlet';
import BaseMyTeamDashlet from '../Dashlets/BaseMyTeamDashlet';

// 02/04/2024 Paul.  Some modules need fixing.  Don't know why. 
// These modules have circular dependency issues and are imported via namespace to allow FixNulledModules() recovery.
import * as All_AuthenticationContext    from '../scripts/adal'                    ;
import * as All_PopupView                from '../views/PopupView'                 ;
import * as All_SplendidDynamic_EditView from '../scripts/SplendidDynamic_EditView';
import * as All_ModuleViewFactory        from '../ModuleViews'                     ;
import * as All_DynamicLayout_Module     from '../scripts/DynamicLayout'           ;
let AuthenticationContext                = All_AuthenticationContext.default            ;
let PopupView                            = All_PopupView.default                        ;
let SplendidDynamic_EditView             = All_SplendidDynamic_EditView.default         ;
let ModuleViewFactory                    = All_ModuleViewFactory.default                ;
let DynamicLayout_Module                 = All_DynamicLayout_Module.DynamicLayout_Module;

// 02/04/2024 Paul.  Modules being null is back. 
const allModules: any[] =
// 3. Scripts
[ { Module: Sql                       , Name: 'Sql'                        }
, { Module: L10n                      , Name: 'L10n'                       }
, { Module: C10n                      , Name: 'C10n'                       }
, { Module: Security                  , Name: 'Security'                   }
, { Module: Credentials               , Name: 'Credentials'                }
, { Module: SplendidCache             , Name: 'SplendidCache'              }
, { Module: SplendidDynamic           , Name: 'SplendidDynamic'            }
, { Module: SplendidDynamic_EditView  , Name: 'SplendidDynamic_EditView'   }
, { Module: SplendidDynamic_DetailView, Name: 'SplendidDynamic_DetailView' }
, { Module: DynamicButtons_LoadLayout , Name: 'DynamicButtons_LoadLayout'  }
, { Module: AuthenticationContext     , Name: 'AuthenticationContext'      }
, { Module: DynamicLayout_Module      , Name: 'DynamicLayout_Module'       }
// 4. Components and Views.
, { Module: DumpSQL                   , Name: 'DumpSQL'                    }
, { Module: SearchView                , Name: 'SearchView'                 }
, { Module: PopupView                 , Name: 'PopupView'                  }
, { Module: DynamicPopupView          , Name: 'DynamicPopupView'           }
, { Module: EditView                  , Name: 'EditView'                   }
, { Module: ListView                  , Name: 'ListView'                   }
, { Module: DetailView                , Name: 'DetailView'                 }
, { Module: DynamicButtons            , Name: 'DynamicButtons'             }
, { Module: ModuleHeader              , Name: 'ModuleHeader'               }
, { Module: ProcessButtons            , Name: 'ProcessButtons'             }
, { Module: ErrorComponent            , Name: 'ErrorComponent'             }
, { Module: Collapsable               , Name: 'Collapsable'                }
, { Module: DetailViewLineItems       , Name: 'DetailViewLineItems'        }
, { Module: DetailViewRelationships   , Name: 'DetailViewRelationships'    }
, { Module: SplendidGrid              , Name: 'SplendidGrid'               }
, { Module: SchedulingGrid            , Name: 'SchedulingGrid'             }
, { Module: ListHeader                , Name: 'ListHeader'                 }
, { Module: SearchTabs                , Name: 'SearchTabs'                 }
, { Module: ExportHeader              , Name: 'ExportHeader'               }
, { Module: PreviewDashboard          , Name: 'PreviewDashboard'           }
, { Module: MassUpdate                , Name: 'MassUpdate'                 }
, { Module: DynamicMassUpdate         , Name: 'DynamicMassUpdate'          }
, { Module: DynamicDetailView         , Name: 'DynamicDetailView'          }
, { Module: DynamicEditView           , Name: 'DynamicEditView'            }
, { Module: DynamicListView           , Name: 'DynamicListView'            }
, { Module: ModuleViewFactory         , Name: 'ModuleViewFactory'          }
, { Module: AuditView                 , Name: 'AuditView'                  }
, { Module: HeaderButtonsFactory      , Name: 'HeaderButtonsFactory'       }
, { Module: SubPanelButtonsFactory    , Name: 'SubPanelButtonsFactory'     }
, { Module: EditViewLineItems         , Name: 'EditViewLineItems'          }
, { Module: ActivitiesPopupView       , Name: 'ActivitiesPopupView'        }
, { Module: PersonalInfoView          , Name: 'PersonalInfoView'           }
, { Module: LayoutTabs                , Name: 'LayoutTabs'                 }
, { Module: BaseMyDashlet             , Name: 'BaseMyDashlet'              }
, { Module: BaseMyFavoriteDashlet     , Name: 'BaseMyFavoriteDashlet'      }
, { Module: BaseMyTeamDashlet         , Name: 'BaseMyTeamDashlet'          }
];

function FixNulledModules()
{
	if ( AuthenticationContext == null || AuthenticationContext === undefined )
	{
		const m = allModules.find(x => x.Name == 'AuthenticationContext');
		m.Module              = All_AuthenticationContext.default.AuthenticationContext;
		AuthenticationContext = All_AuthenticationContext.default.AuthenticationContext;
		console.log((new Date()).toISOString() + ' ' + 'DynamicLayout_Compile.FixNulledModules: AuthenticationContext Restored');
	}
	if ( PopupView == null || PopupView === undefined )
	{
		const m = allModules.find(x => x.Name == 'PopupView');
		m.Module  = All_PopupView.default;
		PopupView = All_PopupView.default;
		console.log((new Date()).toISOString() + ' ' + 'DynamicLayout_Compile.FixNulledModules: PopupView Restored');
	}
	if ( SplendidDynamic_EditView == null || SplendidDynamic_EditView === undefined )
	{
		const m = allModules.find(x => x.Name == 'SplendidDynamic_EditView');
		m.Module                 = All_SplendidDynamic_EditView.default;
		SplendidDynamic_EditView = All_SplendidDynamic_EditView.default;
		console.log((new Date()).toISOString() + ' ' + 'DynamicLayout_Compile.FixNulledModules: SplendidDynamic_EditView Restored');
	}
	if ( ModuleViewFactory == null || ModuleViewFactory === undefined )
	{
		const m = allModules.find(x => x.Name == 'ModuleViewFactory');
		m.Module          = All_ModuleViewFactory.default;
		ModuleViewFactory = All_ModuleViewFactory.default;
		console.log((new Date()).toISOString() + ' ' + 'DynamicLayout_Compile.FixNulledModules: ModuleViewFactory Restored');
	}
	if ( DynamicLayout_Module == null || DynamicLayout_Module === undefined )
	{
		const m = allModules.find(x => x.Name == 'DynamicLayout_Module');
		m.Module             = All_DynamicLayout_Module.DynamicLayout_Module;
		DynamicLayout_Module = All_DynamicLayout_Module.DynamicLayout_Module;
		console.log((new Date()).toISOString() + ' ' + 'DynamicLayout_Compile.FixNulledModules: DynamicLayout_Module Restored');
	}
}

function DumpRequiredModules()
{
	for ( let i = 0; i < allModules.length; i++ )
	{
		if ( allModules[i].Module === null || allModules[i].Module === undefined )
		{
			console.error((new Date()).toISOString() + ' ' + 'DynamicLayout_Compile.DumpRequiredModules: ' + i.toString() + '. ' + Trim(allModules[i].Name) + ' is ' + allModules[i].Module);
		}
	}
}

// Module registry for @babel/standalone compiled components.
// @babel/standalone with es2015 preset outputs require() calls via _interopRequireDefault / _interopRequireWildcard.
// This provides a global require function that resolves against our ESM-imported modules.
// Internal TypeScript modules include __esModule: true so Babel's _interopRequireDefault returns them as-is.
const moduleRegistry: Record<string, any> =
{
	// Third-party libraries (CJS-like — returned directly; Babel's interop wraps them automatically)
	'react'                                      : React,
	'fast-xml-parser'                            : XMLParser,
	'@babel/standalone'                          : Babel,
	'@amcharts/amcharts4/core'                   : am4core,
	'@amcharts/amcharts4/charts'                 : am4charts,
	'mobx'                                       : mobx,
	'react-bootstrap-table-next'                 : BootstrapTable,
	'moment'                                     : moment,
	// Third-party libraries with ESM named/default exports
	'react-bootstrap'                            : { __esModule: true, Modal },
	'query-string'                               : { __esModule: true, default: qs },
	'react-pose'                                 : { __esModule: true, default: posed },
	'mobx-react'                                 : { __esModule: true, observer },
	'@fortawesome/react-fontawesome'             : { __esModule: true, FontAwesomeIcon },
	'react-lifecycle-appear'                     : { __esModule: true, Appear },
	'mobx-react-router'                          : { __esModule: true, RouterStore },
	// Router (RouteComponentProps is a type-only interface — not included as a runtime value)
	'../Router5'                                 : { __esModule: true, SplendidHistory, withRouter, Link, Route, Navigate },
	// Types — classes (exist at runtime)
	'../types/ACL_ACCESS'                        : { __esModule: true, default: ACL_ACCESS },
	'../types/ACL_FIELD_ACCESS'                  : { __esModule: true, default: ACL_FIELD_ACCESS },
	'../types/OrdersLineItemsEditor'             : { __esModule: true, default: OrdersLineItemsEditor },
	// Types — interfaces (erased at compile time; stubs provide module shape for require() resolution)
	'../types/DYNAMIC_BUTTON'                    : { __esModule: true },
	'../types/DETAILVIEWS_RELATIONSHIP'          : { __esModule: true },
	'../types/MODULE'                            : { __esModule: true },
	'../types/RELATIONSHIPS'                     : { __esModule: true },
	'../types/SINGLE_SIGN_ON'                    : { __esModule: true },
	'../types/DASHBOARDS_PANELS'                 : { __esModule: true },
	'../types/SHORTCUT'                          : { __esModule: true },
	'../types/TAB_MENU'                          : { __esModule: true },
	'../types/IDashletProps'                     : { __esModule: true },
	'../types/IOrdersLineItemsEditorProps'       : { __esModule: true },
	'../types/IOrdersLineItemsEditorState'       : { __esModule: true },
	// Types — mixed (classes + interfaces; only class values included)
	'../types/EditComponent'                     : { __esModule: true, EditComponent },
	'../types/DetailComponent'                   : { __esModule: true, DetailComponent },
	'../types/HeaderButtons'                     : { __esModule: true, HeaderButtons },
	// Scripts — default exports
	'../scripts/Sql'                             : { __esModule: true, default: Sql },
	'../scripts/L10n'                            : { __esModule: true, default: L10n },
	'../scripts/C10n'                            : { __esModule: true, default: C10n },
	'../scripts/Security'                        : { __esModule: true, default: Security },
	'../scripts/Credentials'                     : { __esModule: true, default: Credentials },
	'../scripts/SplendidCache'                   : { __esModule: true, default: SplendidCache },
	'../scripts/SplendidDynamic'                 : { __esModule: true, default: SplendidDynamic },
	'../scripts/SplendidDynamic_DetailView'      : { __esModule: true, default: SplendidDynamic_DetailView },
	// Scripts — named exports
	'../scripts/Crm'                             : { __esModule: true, Crm_Config, Crm_Modules, Crm_Teams, Crm_Users },
	'../scripts/Formatting'                      : { __esModule: true, FromJsonDate, ToJsonDate, formatDate, formatCurrency, formatNumber },
	'../scripts/SplendidInitUI'                  : { __esModule: true, sPLATFORM_LAYOUT },
	'../scripts/ModuleUpdate'                    : { __esModule: true, UpdateModule, DeleteModuleItem, DeleteModuleRecurrences, MassDeleteModule, MassUpdateModule, MassSync, MassUnsync, ArchiveMoveData, ArchiveRecoverData, UpdateSavedSearch, DeleteRelatedItem, UpdateRelatedItem, UpdateRelatedList, AdminProcedure, ExecProcedure },
	'../scripts/SplendidRequest'                 : { __esModule: true, CreateSplendidRequest, GetSplendidResult },
	'../scripts/DetailView'                      : { __esModule: true, DetailView_LoadItem, DetailView_LoadLayout, DetailView_LoadPersonalInfo, DetailView_RemoveField, DetailView_HideField, DetailView_FindField, DetailView_GetTabList, DetailView_ActivateTab },
	'../scripts/EditView'                        : { __esModule: true, EditView_LoadItem, EditView_LoadLayout, EditView_ConvertItem, EditView_RemoveField, EditView_InitItem, EditView_FindField, EditView_HideField, EditView_UpdateREPEAT_TYPE, EditView_GetTabList, EditView_ActivateTab },
	'../scripts/Application'                     : { __esModule: true, jsonReactState, Application_GetReactState, Admin_GetReactState, Application_ClearStore, Application_UpdateStoreLastDate, Application_GetReactLoginState },
	'../AppVersion'                              : { __esModule: true, AppName, AppVersion },
	'../scripts/Login'                           : { __esModule: true, AuthenticatedMethod, IsAuthenticated, LoginRedirect, GetUserProfile, GetMyUserProfile, GetUserID, Login, ForgotPassword },
	'../scripts/utility'                         : { __esModule: true, Right, Left, StartsWith, EndsWith, Trim, uuidFast, isEmptyObject, isTouchDevice, base64ArrayBuffer, isMobile: isMobileDevice, isMobileDevice, screenWidth, screenHeight },
	'../scripts/EmailUtils'                      : { __esModule: true, NormalizeDescription, XssFilter },
	'../scripts/ListView'                        : { __esModule: true, ListView_LoadTable, ListView_LoadModule, ListView_LoadLayout, ListView_LoadModulePaginated, ListView_LoadTablePaginated, ListView_LoadTableWithAggregate },
	'../scripts/ConvertLayoutField'              : { __esModule: true, ConvertEditViewFieldToDetailViewField },
	'../scripts/CalendarView'                    : { __esModule: true, GetInviteesActivities },
	'../scripts/DynamicButtons'                  : { __esModule: true, DynamicButtons_LoadLayout },
	// Components and Views — default exports
	'../components/DumpSQL'                      : { __esModule: true, default: DumpSQL },
	'../views/SearchView'                        : { __esModule: true, default: SearchView },
	'../views/DynamicPopupView'                  : { __esModule: true, default: DynamicPopupView },
	'../views/EditView'                          : { __esModule: true, default: EditView },
	'../views/ListView'                          : { __esModule: true, default: ListView },
	'../views/DetailView'                        : { __esModule: true, default: DetailView },
	'../components/DynamicButtons'               : { __esModule: true, default: DynamicButtons },
	'../components/ModuleHeader'                 : { __esModule: true, default: ModuleHeader },
	'../components/ProcessButtons'               : { __esModule: true, default: ProcessButtons },
	'../components/ErrorComponent'               : { __esModule: true, default: ErrorComponent },
	'../components/Collapsable'                  : { __esModule: true, default: Collapsable },
	'../views/DetailViewLineItems'               : { __esModule: true, default: DetailViewLineItems },
	'../views/DetailViewRelationships'           : { __esModule: true, default: DetailViewRelationships },
	'../components/SplendidGrid'                 : { __esModule: true, default: SplendidGrid },
	'../components/SchedulingGrid'               : { __esModule: true, default: SchedulingGrid },
	'../components/ListHeader'                   : { __esModule: true, default: ListHeader },
	'../components/SearchTabs'                   : { __esModule: true, default: SearchTabs },
	'../components/ExportHeader'                 : { __esModule: true, default: ExportHeader },
	'../views/PreviewDashboard'                  : { __esModule: true, default: PreviewDashboard },
	'../views/MassUpdate'                        : { __esModule: true, default: MassUpdate },
	'../views/DynamicMassUpdate'                 : { __esModule: true, default: DynamicMassUpdate },
	'../views/DynamicDetailView'                 : { __esModule: true, default: DynamicDetailView },
	'../views/DynamicEditView'                   : { __esModule: true, default: DynamicEditView },
	'../views/DynamicListView'                   : { __esModule: true, default: DynamicListView },
	'../views/AuditView'                         : { __esModule: true, default: AuditView },
	'../ThemeComponents/HeaderButtonsFactory'     : { __esModule: true, default: HeaderButtonsFactory },
	'../ThemeComponents/SubPanelButtonsFactory'   : { __esModule: true, default: SubPanelButtonsFactory },
	'../views/EditViewLineItems'                 : { __esModule: true, default: EditViewLineItems },
	'../views/ActivitiesPopupView'               : { __esModule: true, default: ActivitiesPopupView },
	'../views/PersonalInfoView'                  : { __esModule: true, default: PersonalInfoView },
	'../components/LayoutTabs'                   : { __esModule: true, default: LayoutTabs },
	'../Dashlets/BaseMyDashlet'                  : { __esModule: true, default: BaseMyDashlet },
	'../Dashlets/BaseMyFavoriteDashlet'          : { __esModule: true, default: BaseMyFavoriteDashlet },
	'../Dashlets/BaseMyTeamDashlet'              : { __esModule: true, default: BaseMyTeamDashlet },
};

// Global require shim for @babel/standalone compiled components.
// @babel/standalone with es2015 preset outputs require() calls in compiled code.
// The 5 circular dependency modules are resolved dynamically (reading current let values)
// because FixNulledModules() can update them after module initialization.
(window as any).require = function(name: string)
{
	// Dynamic resolution for circular dependency modules — reads current let variable values.
	switch ( name )
	{
		case '../scripts/adal'                    : return { __esModule: true, default: AuthenticationContext     };
		case '../views/PopupView'                 : return { __esModule: true, default: PopupView                };
		case '../scripts/SplendidDynamic_EditView': return { __esModule: true, default: SplendidDynamic_EditView };
		case '../ModuleViews'                     : return { __esModule: true, default: ModuleViewFactory        };
		case '../scripts/DynamicLayout'           : return { __esModule: true, DynamicLayout_Module              };
	}
	const mod = moduleRegistry[name];
	if ( mod !== undefined )
	{
		return mod;
	}
	console.error('DynamicLayout_Compile: require() could not resolve module: ' + name);
	return undefined;
};

export async function DynamicLayout_Compile(responseText: string)
{
	// 02/07/2024 Paul.  Fix is still required, but stop dumping to reduce delay. 
	FixNulledModules();
	//DumpRequiredModules();
	let view = await (async () =>
	{
		return eval((Babel.transform(responseText, { presets: ['es2015', 'react', ['stage-0', { decoratorsBeforeExport: true}], ['typescript', { isTSX: true, allExtensions: true }]] })).code);
	})();
	return view;
}
