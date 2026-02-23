/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *********************************************************************************************************************/
#nullable disable
using System;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Currency formatting/conversion utility.
	/// Migrated from SplendidCRM/_code/Currency.cs for .NET 10 ASP.NET Core.
	/// </summary>
	public class Currency
	{
		private Guid   _gID             ;
		private string _sNAME           ;
		private string _sSYMBOL         ;
		private float  _fCONVERSION_RATE;
		private string _sISO4217        ;

		private readonly IMemoryCache _memoryCache;

		public Currency(IMemoryCache memoryCache)
		{
			_memoryCache     = memoryCache;
			_gID             = Guid.Empty;
			_sNAME           = "U.S. Dollar";
			_sSYMBOL         = "$";
			_fCONVERSION_RATE = 1.0f;
			_sISO4217        = "USD";
		}

		public Guid   ID             { get { return _gID             ; } set { _gID             = value; } }
		public string NAME           { get { return _sNAME           ; } set { _sNAME           = value; } }
		public string SYMBOL         { get { return _sSYMBOL         ; } set { _sSYMBOL         = value; } }
		public float  CONVERSION_RATE { get { return _fCONVERSION_RATE; } set { _fCONVERSION_RATE = value; } }
		public string ISO4217        { get { return _sISO4217        ; } set { _sISO4217        = value; } }

		/// <summary>
		/// Creates a Currency instance from a currency ID using cached data.
		/// </summary>
		public static Currency CreateCurrency(IMemoryCache memoryCache, Guid gCURRENCY_ID)
		{
			Currency c = new Currency(memoryCache);
			c.ID = gCURRENCY_ID;
			return c;
		}

		/// <summary>
		/// Converts an amount from one currency to another using conversion rates.
		/// </summary>
		public static Decimal ToCurrency(Decimal dAmount, float fSrcRate, float fDstRate)
		{
			if (fSrcRate == 0.0f) fSrcRate = 1.0f;
			if (fDstRate == 0.0f) fDstRate = 1.0f;
			return dAmount * (Decimal)(fDstRate / fSrcRate);
		}

		/// <summary>
		/// Formats a currency value for display.
		/// </summary>
		public string ToString(Decimal dAmount)
		{
			return _sSYMBOL + dAmount.ToString("N2");
		}
	}
}
