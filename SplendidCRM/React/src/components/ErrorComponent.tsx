/*
 * This program is free software: you can redistribute it and/or modify it under the terms of the 
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3 
 * of the License, or (at your option) any later version.
 * 
 * In accordance with Section 7(b) of the GNU Affero General Public License version 3, 
 * the Appropriate Legal Notices must display the following words on all interactive user interfaces: 
 * "Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved."
 */

// 1. React and fabric. 
import * as React from 'react';
import { Alert } from 'react-bootstrap';
// 2. Store and Types. 
// 3. Scripts. 
// 4. Components and Views. 
interface IErrorComponentProps
{
	error?: any;
}

class ErrorComponent extends React.Component<IErrorComponentProps>
{
	constructor(props: IErrorComponentProps)
	{
		super(props);
	}

	// Sanitize error messages to prevent leaking internal details (SQL table names,
	// stored procedure names, stack traces) to end users.  Only generic messages are
	// shown; the full error is still logged to the browser console for debugging.
	private sanitizeError(error: any): string
	{
		let sRaw: string = '';
		if ( error.message !== undefined )
		{
			sRaw = error.message;
		}
		else if ( typeof error === 'string' )
		{
			sRaw = error;
		}
		else if ( typeof error === 'object' )
		{
			sRaw = JSON.stringify(error);
		}
		// Detect internal implementation details that should not be shown to users
		const sensitivePatterns: RegExp[] = [
			/Invalid object name/i,
			/Incorrect syntax near/i,
			/FETCH statement/i,
			/stored procedure/i,
			/SQL Server/i,
			/SqlException/i,
			/EXECUTE permission/i,
			/transaction \(Process ID/i,
			/deadlock/i,
			/constraint.*violated/i,
		];
		for ( const pattern of sensitivePatterns )
		{
			if ( pattern.test(sRaw) )
			{
				// Log the full error to the console for developers
				console.error('ErrorComponent: sanitized internal error', error);
				return 'An unexpected error occurred. Please try again or contact your administrator.';
			}
		}
		return sRaw;
	}

	public render()
	{
		const { error } = this.props;
		if ( error != undefined && error != null )
		{
			//console.error((new Date()).toISOString() + ' ' + this.constructor.name + '.render', error);
			if (error)
			{
				let sError: string = this.sanitizeError(error);
				return <Alert variant='danger'>{sError}</Alert>;
			}
			return null;
		}
		else
		{
			return null;
		}
	}
}

export default ErrorComponent;
