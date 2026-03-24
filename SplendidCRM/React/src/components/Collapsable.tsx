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
import React, { useState } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';


interface ICollapsableProps
{
	name        : string;
	onToggle?   : (open: boolean) => void;
	initialOpen?: boolean;
	// 01/15/2024 Paul.  Children not automatically included. 
	children    : React.ReactNode;
}

interface ICollapsableState
{
	open: boolean;
}

export default class Collapsable extends React.Component<ICollapsableProps, ICollapsableState>
{
	constructor(props: ICollapsableProps)
	{
		super(props);
		let open = false;
		if ( props.initialOpen !== null && props.initialOpen !== undefined )
		{
			open = props.initialOpen;
		}
		this.state =
		{
			open
		};
	}

	private toggle = () =>
	{
		this.setState({ open: !this.state.open }, () =>
		{
			if (this.props.onToggle)
			{
				this.props.onToggle(this.state.open);
			}
		});
	}
	public render()
	{
		const { open } = this.state;
		// 06/29/2019 Paul.  Only include the children when open so that internal query is not performed unless open. 
		return (
			<React.Fragment>
				<div className='h3Row' style={ {fontSize: '1.5em'} }>
					<h3>
						<i onClick={ this.toggle } style={ {marginRight: '0.5em', cursor: 'pointer', display: 'inline-block', transition: 'transform 0.3s ease', transform: open ? 'rotate(180deg)' : 'rotate(0deg)'} }>
							<FontAwesomeIcon icon={ open ? 'chevron-up' : 'chevron-down' } />
						</i>
						{this.props.name}
					</h3>
				</div>
				<div style={ {overflow: (open ? 'visible' : 'hidden'), height: (open ? 'auto' : '0'), transition: 'height 0.3s ease'} }>
					{ open
					? this.props.children
					: null
					}
				</div>
			</React.Fragment>
		);
	}
}

