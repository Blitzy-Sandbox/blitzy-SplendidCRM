/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved.
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the 
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3 
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; 
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License along with this program. 
 * If not, see <http://www.gnu.org/licenses/>. 
 * 
 * You can contact SplendidCRM Software, Inc. at email address support@splendidcrm.com. 
 * 
 * In accordance with Section 7(b) of the GNU Affero General Public License version 3, 
 * the Appropriate Legal Notices must display the following words on all interactive user interfaces: 
 * "Copyright (C) 2005-2011 SplendidCRM Software, Inc. All rights reserved."
 *********************************************************************************************************************/
// .NET 10 Migration: SplendidCRM/_code/FileBrowser/FileWorkerUtils.cs -> src/SplendidCRM.Core/Integrations/FileBrowser/FileWorkerUtils.cs
// Changes applied per AAP Section 0.5.1 and 0.7.1 migration rules:
//   - REMOVED:  System.Web namespace (replaced with Microsoft.AspNetCore.Http + Microsoft.Extensions.Caching.Memory)
//   - CONVERTED: Static class pattern to instance class with constructor DI (IHttpContextAccessor, IMemoryCache)
//   - CONVERTED: Static LoadImage methods to instance methods
//   - REPLACED:  Legacy ASP.NET form file abstraction with IFormFile (Microsoft.AspNetCore.Http)
//   - REPLACED:  Static HTTP context access with IHttpContextAccessor injection
//   - REPLACED:  Application state dictionary with IMemoryCache injection
//   - REPLACED:  File size obtained via IFormFile.Length (replacing legacy property)
//   - REPLACED:  File content stream obtained via IFormFile.OpenReadStream() (replacing legacy property)
//   - PRESERVED: All business logic, null checks, file size validation, error messages, Path operations
//   - PRESERVED: All inline developer comments (Paul)
using System;
using System.IO;
using System.Data;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM.FileBrowser
{
	public class FileWorkerUtils
	{
		// .NET 10 Migration: DI fields replacing static cache and HTTP context access.
		// IHttpContextAccessor provides the current request context; IMemoryCache provides cached config values.
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache _memoryCache;

		/// <summary>
		/// Initializes a new instance of <see cref="FileWorkerUtils"/> with required DI services.
		/// </summary>
		/// <param name="httpContextAccessor">ASP.NET Core HTTP context accessor (replaces static HTTP context).</param>
		/// <param name="memoryCache">In-memory cache service (replaces Application state dictionary).</param>
		public FileWorkerUtils(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache = memoryCache;
		}

		// 11/06/2010 Paul.  Move LoadFile() to Crm.EmailImages. 

		// .NET 10 Migration: Converted from static to instance method; DI replaces static HTTP context access.
		public void LoadImage(ref Guid gImageID, ref string sFILENAME, IDbTransaction trn)
		{
			// 04/26/2012 Paul.  CKEditor change the name to upload. 
			LoadImage(ref gImageID, ref sFILENAME, "upload", trn);
		}

		// 08/09/2009 Paul.  We need to allow the field name to be a parameter so that this code can be reused. 
		// .NET 10 Migration: Converted from static to instance method; DI replaces static HTTP context access.
		public void LoadImage(ref Guid gImageID, ref string sFILENAME, string sHTML_FIELD_NAME, IDbTransaction trn)
		{
			gImageID = Guid.Empty;
			// .NET 10 Migration: IFormFile is the ASP.NET Core form file interface;
			// retrieved via _httpContextAccessor from the current request's form files collection.
			IFormFile pstIMAGE = _httpContextAccessor.HttpContext?.Request.Form.Files[sHTML_FIELD_NAME];
			if ( pstIMAGE != null )
			{
				// .NET 10 Migration: IFormFile.Length provides the uploaded file size in bytes.
				long lFileSize      = pstIMAGE.Length;
				// .NET 10 Migration: _memoryCache.Get() reads the cached config value from IMemoryCache.
				long lUploadMaxSize = Sql.ToLong(_memoryCache.Get("CONFIG.upload_maxsize"));
				if ( (lUploadMaxSize > 0) && (lFileSize > lUploadMaxSize) )
				{
					throw(new Exception("ERROR: uploaded file was too big: max filesize: " + lUploadMaxSize.ToString()));
				}
				// 04/13/2005 Paul.  File may not have been provided. 
				if ( pstIMAGE.FileName.Length > 0 )
				{
					// IFormFile.FileName and IFormFile.ContentType have identical semantics to the legacy form file API.
					sFILENAME              = Path.GetFileName (pstIMAGE.FileName);
					string sFILE_EXT       = Path.GetExtension(sFILENAME);
					string sFILE_MIME_TYPE = pstIMAGE.ContentType;
					
					SqlProcs.spEMAIL_IMAGES_Insert
						( ref gImageID
						, Guid.Empty // gParentID
						, sFILENAME
						, sFILE_EXT
						, sFILE_MIME_TYPE
						, trn
						);
					// 09/06/2008 Paul.  PostgreSQL does not require that we stream the bytes, so lets explore doing this for all platforms. 
					// 10/18/2009 Paul.  Move blob logic to LoadFile. 
					// .NET 10 Migration: IFormFile.OpenReadStream() provides the upload stream for binary persistence.
					Crm.EmailImages.LoadFile(gImageID, pstIMAGE.OpenReadStream(), trn);
				}
			}
		}
	}
}
