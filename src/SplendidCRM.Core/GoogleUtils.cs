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
// .NET 10 Migration: SplendidCRM/_code/GoogleUtils.cs → src/SplendidCRM.Core/GoogleUtils.cs
// Changes applied:
//   - REMOVED: using System.Web; (HttpUtility.UrlEncode)
//   - REMOVED: using System.Web.Script.Serialization; (JavaScriptSerializer)
//   - ADDED:   using System.Net; (WebUtility.UrlEncode replaces HttpUtility.UrlEncode)
//   - ADDED:   using System.Text.Json; (JsonSerializer replaces JavaScriptSerializer)
//   - REPLACED: HttpUtility.UrlEncode(Address) → WebUtility.UrlEncode(Address)
//   - REPLACED: JavaScriptSerializer + json.Deserialize<T>() → JsonSerializer.Deserialize<T>() with IncludeFields=true
//   - PRESERVED: All business logic, all class structures, all method signatures, namespace SplendidCRM
//   - NOTE: Google.Apis.Contacts.v3.Data.StructuredPostalAddress is defined as a stub in GoogleApps.cs
#nullable disable
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

using Google.Apis.Contacts.v3.Data;

namespace SplendidCRM
{
	/// <summary>
	/// Base extension details class providing a virtual string indexer for field access.
	/// Used as the base class for <see cref="AddressDetails"/> to allow polymorphic field access.
	/// </summary>
	public class ExtensionDetails
	{
		/// <summary>
		/// Gets or sets a field value by name. Virtual — overridden in <see cref="AddressDetails"/>.
		/// </summary>
		public virtual string this[string sFieldName]
		{
			get { return null; }
			set { }
		}
	}

	/// <summary>
	/// Holds address and contact details parsed from a Google Maps geocoding API response.
	/// Provides a named string indexer for field-name-based access, mapping multiple CRM address
	/// field name variants (BILLING_, SHIPPING_, PRIMARY_, ALT_) to the same underlying fields.
	/// </summary>
	public class AddressDetails : ExtensionDetails
	{
		public string NAME              ;
		public string FIRST_NAME        ;
		public string LAST_NAME         ;
		public string ADDRESS_STREET    ;
		public string ADDRESS_CITY      ;
		public string ADDRESS_COUNTY    ;
		public string ADDRESS_TOWN      ;
		public string ADDRESS_STATE     ;
		public string ADDRESS_POSTALCODE;
		public string ADDRESS_COUNTRY   ;
		public string EMAIL1            ;
		public string EMAIL2            ;
		public string PHONE1            ;
		public string PHONE2            ;
		public string WEBSITE           ;
		public string EDIT_VIEW         ;
		public string LocationStatus    ;
		public string SplendidCRM_URL   ;
		public string Accuracy          ;

		/// <summary>
		/// Initializes a new <see cref="AddressDetails"/> instance with all fields set to <see cref="String.Empty"/>.
		/// </summary>
		public AddressDetails()
		{
			NAME               = String.Empty;
			FIRST_NAME         = String.Empty;
			LAST_NAME          = String.Empty;
			ADDRESS_STREET     = String.Empty;
			ADDRESS_CITY       = String.Empty;
			ADDRESS_COUNTY     = String.Empty;
			ADDRESS_TOWN       = String.Empty;
			ADDRESS_STATE      = String.Empty;
			ADDRESS_POSTALCODE = String.Empty;
			ADDRESS_COUNTRY    = String.Empty;
			EMAIL1             = String.Empty;
			EMAIL2             = String.Empty;
			PHONE1             = String.Empty;
			PHONE2             = String.Empty;
			WEBSITE            = String.Empty;
			EDIT_VIEW          = String.Empty;
			LocationStatus     = String.Empty;
			SplendidCRM_URL    = String.Empty;
			Accuracy           = String.Empty;
		}

		/// <summary>
		/// Gets or sets a field value by CRM field name. Maps billing, shipping, primary, and alt address
		/// variants to the same underlying address fields to support all CRM module address conventions.
		/// </summary>
		public override string this[string sFieldName]
		{
			get
			{
				string sValue = String.Empty;
				switch ( sFieldName.ToUpper() )
				{
					case "NAME"                       :  sValue = this.NAME              ;  break;
					case "FIRST_NAME"                 :  sValue = this.FIRST_NAME        ;  break;
					case "LAST_NAME"                  :  sValue = this.LAST_NAME         ;  break;
					case "BILLING_ADDRESS_STREET"     :  sValue = this.ADDRESS_STREET    ;  break;
					case "BILLING_ADDRESS_CITY"       :  sValue = this.ADDRESS_CITY      ;  break;
					case "BILLING_ADDRESS_STATE"      :  sValue = this.ADDRESS_STATE     ;  break;
					case "BILLING_ADDRESS_POSTALCODE" :  sValue = this.ADDRESS_POSTALCODE;  break;
					case "BILLING_ADDRESS_COUNTRY"    :  sValue = this.ADDRESS_COUNTRY   ;  break;
					case "SHIPPING_ADDRESS_STREET"    :  sValue = this.ADDRESS_STREET    ;  break;
					case "SHIPPING_ADDRESS_CITY"      :  sValue = this.ADDRESS_CITY      ;  break;
					case "SHIPPING_ADDRESS_STATE"     :  sValue = this.ADDRESS_STATE     ;  break;
					case "SHIPPING_ADDRESS_POSTALCODE":  sValue = this.ADDRESS_POSTALCODE;  break;
					case "SHIPPING_ADDRESS_COUNTRY"   :  sValue = this.ADDRESS_COUNTRY   ;  break;
					case "PRIMARY_ADDRESS_STREET"     :  sValue = this.ADDRESS_STREET    ;  break;
					case "PRIMARY_ADDRESS_CITY"       :  sValue = this.ADDRESS_CITY      ;  break;
					case "PRIMARY_ADDRESS_STATE"      :  sValue = this.ADDRESS_STATE     ;  break;
					case "PRIMARY_ADDRESS_POSTALCODE" :  sValue = this.ADDRESS_POSTALCODE;  break;
					case "PRIMARY_ADDRESS_COUNTRY"    :  sValue = this.ADDRESS_COUNTRY   ;  break;
					case "ALT_ADDRESS_STREET"         :  sValue = this.ADDRESS_STREET    ;  break;
					case "ALT_ADDRESS_CITY"           :  sValue = this.ADDRESS_CITY      ;  break;
					case "ALT_ADDRESS_STATE"          :  sValue = this.ADDRESS_STATE     ;  break;
					case "ALT_ADDRESS_POSTALCODE"     :  sValue = this.ADDRESS_POSTALCODE;  break;
					case "ALT_ADDRESS_COUNTRY"        :  sValue = this.ADDRESS_COUNTRY   ;  break;
					case "EMAIL1"                     :  sValue = this.EMAIL1            ;  break;
					case "EMAIL2"                     :  sValue = this.EMAIL2            ;  break;
					case "PHONE_OFFICE"               :  sValue = this.PHONE1            ;  break;
					case "PHONE_WORK"                 :  sValue = this.PHONE1            ;  break;
					case "PHONE_FAX"                  :  sValue = this.PHONE2            ;  break;
					case "PHONE_MOBILE"               :  sValue = this.PHONE2            ;  break;
					case "WEBSITE"                    :  sValue = this.WEBSITE           ;  break;
				}
				return sValue;
			}
			set
			{
				switch ( sFieldName.ToUpper() )
				{
					case "NAME"                       :  this.NAME               = value;  break;
					case "FIRST_NAME"                 :  this.FIRST_NAME         = value;  break;
					case "LAST_NAME"                  :  this.LAST_NAME          = value;  break;
					case "BILLING_ADDRESS_STREET"     :  this.ADDRESS_STREET     = value;  break;
					case "BILLING_ADDRESS_CITY"       :  this.ADDRESS_CITY       = value;  break;
					case "BILLING_ADDRESS_STATE"      :  this.ADDRESS_STATE      = value;  break;
					case "BILLING_ADDRESS_POSTALCODE" :  this.ADDRESS_POSTALCODE = value;  break;
					case "BILLING_ADDRESS_COUNTRY"    :  this.ADDRESS_COUNTRY    = value;  break;
					case "SHIPPING_ADDRESS_STREET"    :  this.ADDRESS_STREET     = value;  break;
					case "SHIPPING_ADDRESS_CITY"      :  this.ADDRESS_CITY       = value;  break;
					case "SHIPPING_ADDRESS_STATE"     :  this.ADDRESS_STATE      = value;  break;
					case "SHIPPING_ADDRESS_POSTALCODE":  this.ADDRESS_POSTALCODE = value;  break;
					case "SHIPPING_ADDRESS_COUNTRY"   :  this.ADDRESS_COUNTRY    = value;  break;
					case "PRIMARY_ADDRESS_STREET"     :  this.ADDRESS_STREET     = value;  break;
					case "PRIMARY_ADDRESS_CITY"       :  this.ADDRESS_CITY       = value;  break;
					case "PRIMARY_ADDRESS_STATE"      :  this.ADDRESS_STATE      = value;  break;
					case "PRIMARY_ADDRESS_POSTALCODE" :  this.ADDRESS_POSTALCODE = value;  break;
					case "PRIMARY_ADDRESS_COUNTRY"    :  this.ADDRESS_COUNTRY    = value;  break;
					case "ALT_ADDRESS_STREET"         :  this.ADDRESS_STREET     = value;  break;
					case "ALT_ADDRESS_CITY"           :  this.ADDRESS_CITY       = value;  break;
					case "ALT_ADDRESS_STATE"          :  this.ADDRESS_STATE      = value;  break;
					case "ALT_ADDRESS_POSTALCODE"     :  this.ADDRESS_POSTALCODE = value;  break;
					case "ALT_ADDRESS_COUNTRY"        :  this.ADDRESS_COUNTRY    = value;  break;
					case "EMAIL1"                     :  this.EMAIL1             = value;  break;
					case "EMAIL2"                     :  this.EMAIL2             = value;  break;
					case "PHONE_OFFICE"               :  this.PHONE1             = value;  break;
					case "PHONE_WORK"                 :  this.PHONE1             = value;  break;
					case "PHONE_FAX"                  :  this.PHONE2             = value;  break;
					case "PHONE_MOBILE"               :  this.PHONE2             = value;  break;
					case "WEBSITE"                    :  this.WEBSITE            = value;  break;
				}
			}
		}
	}

	#region Google Maps Response
	/*
	{
		"name": "1600 Amphitheatre Parkway, Mountain View, CA",
		"Status": 
		{
			"code": 200,
			"request": "geocode"
		},
		"Placemark": 
		[
			{
				"id": "p1",
				"address": "1600 Amphitheatre Pkwy, Mountain View, CA 94043, USA",
				"AddressDetails": 
				{
					"Accuracy" : 8,
					"Country" : 
					{
						"AdministrativeArea" : 
						{
							"AdministrativeAreaName" : "CA",
							"SubAdministrativeArea" : 
							{
								"Locality" : 
								{
									"LocalityName" : "Mountain View",
									"PostalCode" : 
									{
										"PostalCodeNumber" : "94043"
									},
									"Thoroughfare" : 
									{
										"ThoroughfareName" : "1600 Amphitheatre Pkwy"
									}
								},
								"SubAdministrativeAreaName" : "Santa Clara"
							}
						},
						"CountryName" : "USA",
						"CountryNameCode" : "US"
					}
				},
				"ExtendedData": 
				{
					"LatLonBox": 
					{
						"north": 37.4247703,
						"south": 37.4184751,
						"east": -122.0808787,
						"west": -122.0871739
					}
				},
				"Point": 
				{
					"coordinates": [ -122.0840263, 37.4216227, 0 ]
				}
			}
		]
	}
	*/

	/// <summary>
	/// Deserializable model for the Google Maps Geocoding API V2 JSON response structure.
	/// V2 API has been deprecated (as of 08/26/2011) but the model is preserved for backward compatibility.
	/// Contains nested classes matching the Google Maps V2 JSON hierarchy exactly.
	/// </summary>
	public class GoogleMapsResponseV2
	{
		/// <summary>
		/// Google Geocoding API V2 status codes as defined in the Google Maps API reference.
		/// http://code.google.com/apis/maps/documentation/mapplets/reference.html
		/// </summary>
		public enum GGeoStatusCode
		{
			G_GEO_SUCCESS             = 200,  // No errors occurred; the address was successfully parsed and its geocode has been returned.  
			G_GEO_BAD_REQUEST         = 400,  // A directions request could not be successfully parsed.  
			G_GEO_SERVER_ERROR        = 500,  // A geocoding or directions request could not be successfully processed, yet the exact reason for the failure is not known.  
			G_GEO_MISSING_QUERY       = 601,  // The HTTP q parameter was either missing or had no value. For geocoding requests, this means that an empty address was specified as input. For directions requests, this means that no query was specified in the input.  
			G_GEO_UNKNOWN_ADDRESS     = 602,  // No corresponding geographic location could be found for the specified address. This may be due to the fact that the address is relatively new, or it may be incorrect.  
			G_GEO_UNAVAILABLE_ADDRESS = 603,  // The geocode for the given address or the route for the given directions query cannot be returned due to legal or contractual reasons.  
			G_GEO_UNKNOWN_DIRECTIONS  = 604,  // The GDirections object could not compute directions between the points mentioned in the query. This is usually because there is no route available between the two points, or because we do not have data for routing in that region.  
			G_GEO_BAD_KEY             = 610, 
		}

		/// <summary>Geocoding request status containing the status code and request type.</summary>
		public class MapsStatus
		{
			public GGeoStatusCode code   ;
			public string         request;
		}

		/// <summary>Geocoding placemark entry containing address details, extended data, and point coordinates.</summary>
		public class MapsPlacemark
		{
			/// <summary>Nested address details including accuracy and country hierarchy.</summary>
			public class MapsAddressDetails
			{
				/// <summary>Country-level address hierarchy with administrative area breakdown.</summary>
				public class MapsCountry
				{
					/// <summary>State/province level administrative area.</summary>
					public class MapsAdministrativeArea
					{
						/// <summary>County-level sub-administrative area with locality.</summary>
						public class MapsSubAdministrativeArea
						{
							/// <summary>City/town locality with thoroughfare and postal code.</summary>
							public class MapsLocality
							{
								/// <summary>Postal (zip) code for the locality.</summary>
								public class MapsPostalCode
								{
									public string PostalCodeNumber;
								}
								/// <summary>Street-level thoroughfare name.</summary>
								public class MapsThoroughfare
								{
									public string ThoroughfareName;
								}
								public string           LocalityName;
								public MapsPostalCode   PostalCode  ;
								public MapsThoroughfare Thoroughfare;
							}
							public MapsLocality Locality                 ;
							public string       SubAdministrativeAreaName;
						}
						public string                    AdministrativeAreaName;
						public MapsSubAdministrativeArea SubAdministrativeArea ;
					}
					public MapsAdministrativeArea AdministrativeArea;
					public string                 CountryName       ;
					public string                 CountryNameCode   ;
				}
				public int         Accuracy;
				public MapsCountry Country ;
			}
			/// <summary>Extended data including bounding box coordinates for the result.</summary>
			public class MapsExtendedData
			{
				/// <summary>Lat/lon bounding box with north, south, east, west coordinates.</summary>
				public class MapsLatLonBox
				{
					public double north;
					public double south;
					public double east ;
					public double west ;
				}
				public MapsLatLonBox LatLonBox;
			}
			/// <summary>Point coordinate array [longitude, latitude, altitude].</summary>
			public class MapsPoint
			{
				public double[] coordinates;
			}
			public string             id            ;
			public string             address       ;
			public MapsAddressDetails AddressDetails;
			public MapsExtendedData   ExtendedData  ;
			public MapsPoint          Point         ;
		}
		public string          name     ;
		public MapsStatus      Status   ;
		public MapsPlacemark[] Placemark;
	}

	/*
	{
	   "results" : [
	      {
	         "address_components" : [
	            {
	               "long_name" : "400",
	               "short_name" : "400",
	               "types" : [ "subpremise" ]
	            },
	            {
	               "long_name" : "1000",
	               "short_name" : "1000",
	               "types" : [ "street_number" ]
	            },
	            {
	               "long_name" : "E Woodfield Rd",
	               "short_name" : "E Woodfield Rd",
	               "types" : [ "route" ]
	            },
	            {
	               "long_name" : "Schaumburg",
	               "short_name" : "Schaumburg",
	               "types" : [ "locality", "political" ]
	            },
	            {
	               "long_name" : "Schaumburg",
	               "short_name" : "Schaumburg",
	               "types" : [ "administrative_area_level_3", "political" ]
	            },
	            {
	               "long_name" : "Cook",
	               "short_name" : "Cook",
	               "types" : [ "administrative_area_level_2", "political" ]
	            },
	            {
	               "long_name" : "Illinois",
	               "short_name" : "IL",
	               "types" : [ "administrative_area_level_1", "political" ]
	            },
	            {
	               "long_name" : "United States",
	               "short_name" : "US",
	               "types" : [ "country", "political" ]
	            },
	            {
	               "long_name" : "60173",
	               "short_name" : "60173",
	               "types" : [ "postal_code" ]
	            }
	         ],
	         "formatted_address" : "1000 E Woodfield Rd #400, Schaumburg, IL 60173, USA",
	         "geometry" : {
	            "location" : {
	               "lat" : 42.04292710,
	               "lng" : -88.05768089999999
	            },
	            "location_type" : "APPROXIMATE",
	            "viewport" : {
	               "northeast" : {
	                  "lat" : 42.04427608029150,
	                  "lng" : -88.05633191970850
	               },
	               "southwest" : {
	                  "lat" : 42.04157811970850,
	                  "lng" : -88.05902988029150
	               }
	            }
	         },
	         "partial_match" : true,
	         "types" : [ "subpremise" ]
	      }
	   ],
	   "status" : "OK"
	}
	*/

	/// <summary>
	/// Deserializable model for the Google Maps Geocoding API V3 JSON response structure.
	/// http://code.google.com/apis/maps/documentation/geocoding/
	/// Contains nested classes matching the Google Maps V3 JSON hierarchy exactly.
	/// </summary>
	public class GoogleMapsResponseV3
	{
		/// <summary>
		/// Google Geocoding API V3 string-based status codes.
		/// http://code.google.com/apis/maps/documentation/geocoding/#StatusCodes
		/// </summary>
		public enum GGeoStatusCode
		{
			OK,               // "OK"               indicates that no errors occurred; the address was successfully parsed and at least one geocode was returned.
			ZERO_RESULTS,     // "ZERO_RESULTS"     indicates that the geocode was successful but returned no results. This may occur if the geocode was passed a non-existent address or a latlng in a remote location.
			OVER_QUERY_LIMIT, // "OVER_QUERY_LIMIT" indicates that you are over your quota.
			REQUEST_DENIED,   // "REQUEST_DENIED"   indicates that your request was denied, generally because of lack of a sensor parameter.
			INVALID_REQUEST   // "INVALID_REQUEST"  generally indicates that the query (address or latlng) is missing.
		}

		/// <summary>A single geocoding result containing address components and geometry.</summary>
		public class V3Results
		{
			/// <summary>A single address component (street number, route, locality, etc.) with type tags.</summary>
			public class AddressComponent
			{
				public string   long_name ;
				public string   short_name;
				public string[] types     ;
			}
			/// <summary>Geographic geometry for the result including location point and viewport bounding box.</summary>
			public class Geometry
			{
				/// <summary>Lat/lng coordinate point.</summary>
				public class Location
				{
					public double lat;
					public double lng;
				}
				/// <summary>Viewport bounding box with northeast and southwest corners.</summary>
				public class Viewport
				{
					public Location southwest;
					public Location northeast;
				}
				public Location location     ;
				public string   location_type;
				public Viewport viewport     ;
			}

			public string[]           types             ;
			public string             formatted_address ;
			public AddressComponent[] address_components;
			public Geometry           geometry          ;
		}
		public string      status ;
		public V3Results[] results;
	}
	#endregion

	/// <summary>
	/// Google API helper utilities for address geocoding and formatting.
	/// Provides methods to convert street addresses to structured components using the Google Maps
	/// Geocoding API (V2 and V3) and to build formatted address strings from structured postal addresses.
	/// 
	/// .NET 10 Migration Notes:
	/// - REMOVED: System.Web.HttpUtility.UrlEncode → replaced with System.Net.WebUtility.UrlEncode
	/// - REMOVED: System.Web.Script.Serialization.JavaScriptSerializer → replaced with System.Text.Json.JsonSerializer
	/// - JsonSerializerOptions configured with IncludeFields=true and PropertyNameCaseInsensitive=true
	///   to match the permissive deserialization behavior of the legacy JavaScriptSerializer.
	/// - All public method signatures preserved exactly for backward compatibility.
	/// </summary>
	public class GoogleUtils
	{
		/// <summary>
		/// Shared JSON deserialization options configured to:
		/// - IncludeFields = true: required because GoogleMapsResponseV2/V3 use public fields (not properties)
		/// - PropertyNameCaseInsensitive = true: matches the case-insensitive behavior of JavaScriptSerializer
		/// These options replace the behavior of System.Web.Script.Serialization.JavaScriptSerializer
		/// which was the original JSON deserializer in the .NET Framework implementation.
		/// </summary>
		private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			IncludeFields               = true,
		};

		// 08/26/2011 Paul.  Geocoding API V2 has been deprecated.
		/// <summary>
		/// Converts a free-form address string to structured address components using the Google Maps
		/// Geocoding API V2. Populates the provided <paramref name="info"/> with street, city, state,
		/// postal code, country, and geocode accuracy. V2 API is deprecated — prefer <see cref="ConvertAddressV3"/>.
		/// </summary>
		/// <param name="sGoogleMapsKey">The Google Maps API key for the V2 geocoding request.</param>
		/// <param name="Address">The free-form address string to geocode.</param>
		/// <param name="info">Output address details populated from the geocoding response.</param>
		public static void ConvertAddressV2(string sGoogleMapsKey, string Address, ref AddressDetails info)
		{
			// 08/26/2011 Paul.  Geocoding API V2 has been deprecated.
			// http://code.google.com/apis/maps/documentation/geocoding/v2/index.html
			// .NET 10 Migration: WebUtility.UrlEncode replaces HttpUtility.UrlEncode (System.Web removed)
			string sURL = "http://maps.google.com/maps/geo?q=" + WebUtility.UrlEncode(Address) + "&output=json&oe=utf8&sensor=false&key=" + sGoogleMapsKey;
			HttpWebRequest objRequest = (HttpWebRequest) WebRequest.Create(sURL);
			objRequest.Headers.Add("cache-control", "no-cache");
			objRequest.KeepAlive         = false;
			objRequest.AllowAutoRedirect = false;
			objRequest.Timeout           = 15000;  //15 seconds
			objRequest.Method            = "GET";

			// 01/11/2011 Paul.  Make sure to dispose of the response object as soon as possible. 
			using ( HttpWebResponse objResponse = (HttpWebResponse) objRequest.GetResponse() )
			{
				if ( objResponse != null )
				{
					if ( objResponse.StatusCode == HttpStatusCode.OK || objResponse.StatusCode == HttpStatusCode.Found )
					{
						using ( StreamReader readStream = new StreamReader(objResponse.GetResponseStream(), Encoding.UTF8) )
						{
							string sJsonResponse = readStream.ReadToEnd();
							// .NET 10 Migration: JsonSerializer.Deserialize replaces JavaScriptSerializer.Deserialize.
							// _jsonOptions includes IncludeFields=true (fields, not properties) and
							// PropertyNameCaseInsensitive=true (matches JavaScriptSerializer behavior).
							GoogleMapsResponseV2 resp = JsonSerializer.Deserialize<GoogleMapsResponseV2>(sJsonResponse, _jsonOptions);
							if ( resp.Placemark != null && resp.Placemark.Length > 0 )
							{
								if ( resp.Placemark[0].AddressDetails != null && resp.Placemark[0].AddressDetails.Country != null )
								{
									// http://code.google.com/apis/maps/documentation/javascript/v2/reference.html#GGeoAddressAccuracy
									info.Accuracy = resp.Placemark[0].AddressDetails.Accuracy.ToString();
									info.ADDRESS_COUNTRY = resp.Placemark[0].AddressDetails.Country.CountryName;
									if ( resp.Placemark[0].AddressDetails.Country.AdministrativeArea != null )
									{
										info.ADDRESS_STATE = resp.Placemark[0].AddressDetails.Country.AdministrativeArea.AdministrativeAreaName;
										if ( resp.Placemark[0].AddressDetails.Country.AdministrativeArea.SubAdministrativeArea != null && resp.Placemark[0].AddressDetails.Country.AdministrativeArea.SubAdministrativeArea.Locality != null )
										{
											info.ADDRESS_CITY = resp.Placemark[0].AddressDetails.Country.AdministrativeArea.SubAdministrativeArea.Locality.LocalityName;
											if ( resp.Placemark[0].AddressDetails.Country.AdministrativeArea.SubAdministrativeArea.Locality.Thoroughfare != null )
											{
												info.ADDRESS_STREET = resp.Placemark[0].AddressDetails.Country.AdministrativeArea.SubAdministrativeArea.Locality.Thoroughfare.ThoroughfareName;
											}
											if ( resp.Placemark[0].AddressDetails.Country.AdministrativeArea.SubAdministrativeArea.Locality.PostalCode != null )
												info.ADDRESS_POSTALCODE  = resp.Placemark[0].AddressDetails.Country.AdministrativeArea.SubAdministrativeArea.Locality.PostalCode.PostalCodeNumber;
										}
									}
								}
							}
							// http://code.google.com/apis/maps/documentation/mapplets/reference.html
							switch ( resp.Status.code )
							{
								case GoogleMapsResponseV2.GGeoStatusCode.G_GEO_SUCCESS            :  info.LocationStatus = "Success"            ;  break;
								case GoogleMapsResponseV2.GGeoStatusCode.G_GEO_BAD_REQUEST        :  info.LocationStatus = "Bad request"        ;  break;
								case GoogleMapsResponseV2.GGeoStatusCode.G_GEO_SERVER_ERROR       :  info.LocationStatus = "Server error"       ;  break;
								case GoogleMapsResponseV2.GGeoStatusCode.G_GEO_MISSING_QUERY      :  info.LocationStatus = "Missing query"      ;  break;
								case GoogleMapsResponseV2.GGeoStatusCode.G_GEO_UNKNOWN_ADDRESS    :  info.LocationStatus = "Unknown address"    ;  break;
								case GoogleMapsResponseV2.GGeoStatusCode.G_GEO_UNAVAILABLE_ADDRESS:  info.LocationStatus = "Unavailable address";  break;
								case GoogleMapsResponseV2.GGeoStatusCode.G_GEO_UNKNOWN_DIRECTIONS :  info.LocationStatus = "Unknown directions" ;  break;
								case GoogleMapsResponseV2.GGeoStatusCode.G_GEO_BAD_KEY            :  info.LocationStatus = "Bad key"            ;  break;
							}
						}
					}
					else
					{
						info.LocationStatus = objResponse.StatusCode.ToString();
					}
				}
			}
		}

		/// <summary>
		/// Converts a free-form address string to structured address components using the Google Maps
		/// Geocoding API V3. Populates the provided <paramref name="info"/> with street, city, state,
		/// postal code, country, county, town, and location accuracy type.
		/// </summary>
		/// <param name="Address">The free-form address string to geocode.</param>
		/// <param name="bShortStateName">
		/// If true, uses the short (abbreviated) state name (e.g., "IL") when available;
		/// otherwise uses the long name (e.g., "Illinois").
		/// </param>
		/// <param name="bShortCountryName">
		/// If true, uses the short (ISO) country code (e.g., "US") when available;
		/// otherwise uses the long name (e.g., "United States").
		/// </param>
		/// <param name="info">Output address details populated from the geocoding response.</param>
		public static void ConvertAddressV3(string Address, bool bShortStateName, bool bShortCountryName, ref AddressDetails info)
		{
			// 08/26/2011 Paul.  Geocoding API V3.
			// http://code.google.com/apis/maps/documentation/geocoding/
			// .NET 10 Migration: WebUtility.UrlEncode replaces HttpUtility.UrlEncode (System.Web removed)
			string sURL = "http://maps.googleapis.com/maps/api/geocode/json?address=" + WebUtility.UrlEncode(Address) + "&sensor=false";
			HttpWebRequest objRequest = (HttpWebRequest) WebRequest.Create(sURL);
			objRequest.Headers.Add("cache-control", "no-cache");
			objRequest.KeepAlive         = false;
			objRequest.AllowAutoRedirect = false;
			objRequest.Timeout           = 15000;  //15 seconds
			objRequest.Method            = "GET";

			// 01/11/2011 Paul.  Make sure to dispose of the response object as soon as possible. 
			using ( HttpWebResponse objResponse = (HttpWebResponse) objRequest.GetResponse() )
			{
				if ( objResponse != null )
				{
					if ( objResponse.StatusCode == HttpStatusCode.OK || objResponse.StatusCode == HttpStatusCode.Found )
					{
						using ( StreamReader readStream = new StreamReader(objResponse.GetResponseStream(), Encoding.UTF8) )
						{
							string sJsonResponse = readStream.ReadToEnd();
							// .NET 10 Migration: JsonSerializer.Deserialize replaces JavaScriptSerializer.Deserialize.
							// _jsonOptions includes IncludeFields=true (fields, not properties) and
							// PropertyNameCaseInsensitive=true (matches JavaScriptSerializer behavior).
							GoogleMapsResponseV3 resp = JsonSerializer.Deserialize<GoogleMapsResponseV3>(sJsonResponse, _jsonOptions);
							if ( resp.status == "OK" )
							{
								if ( resp.results != null && resp.results.Length > 0 )
								{
									GoogleMapsResponseV3.V3Results result = resp.results[0];
									if ( result.address_components != null )
									{
										// 08/26/2011 Paul.  Subpremise is the suite number. 
										string sSubpremise   = String.Empty;
										string sStreetNumber = String.Empty;
										string sRoute        = String.Empty;
										foreach ( GoogleMapsResponseV3.V3Results.AddressComponent adr in result.address_components )
										{
											if ( adr.types != null && adr.types.Length > 0 )
											{
												foreach ( string type in adr.types )
												{
													// 08/26/2011 Paul.  If this set is a postal prefix, then skip the rest of the types. 
													if ( type == "postal_code_prefix" )
														break;
													switch ( type )
													{
														case "subpremise"                 :  sSubpremise             = adr.long_name;  break;
														case "street_number"              :  sStreetNumber           = adr.long_name;  break;
														case "route"                      :  sRoute                  = adr.long_name;  break;
														case "locality"                   :  info.ADDRESS_CITY       = adr.long_name;  break;
														case "administrative_area_level_1":  info.ADDRESS_STATE      = (bShortStateName && !Sql.IsEmptyString(adr.short_name)) ? adr.short_name : adr.long_name;  break;
														case "administrative_area_level_2":  info.ADDRESS_COUNTY     = adr.long_name;  break;
														case "administrative_area_level_3":  info.ADDRESS_TOWN       = adr.long_name;  break;
														case "country"                    :  info.ADDRESS_COUNTRY    = (bShortCountryName && !Sql.IsEmptyString(adr.short_name)) ? adr.short_name : adr.long_name;  break;
														case "postal_code"                :  info.ADDRESS_POSTALCODE = adr.long_name;  break;
													}
												}
											}
										}
										info.ADDRESS_STREET = (sStreetNumber + " " + sRoute).Trim();
										if ( !Sql.IsEmptyString(sSubpremise) )
											info.ADDRESS_STREET += " #" + sSubpremise;
										if ( result.geometry != null )
										{
											info.Accuracy = result.geometry.location_type;
										}
									}
								}
							}
							// http://code.google.com/apis/maps/documentation/geocoding/#StatusCodes
							switch ( resp.status )
							{
								case "OK"              :  info.LocationStatus = "Success"         ;  break;
								case "ZERO_RESULTS"    :  info.LocationStatus = "zero results"    ;  break;
								case "OVER_QUERY_LIMIT":  info.LocationStatus = "over query limit";  break;
								case "REQUEST_DENIED"  :  info.LocationStatus = "request denied"  ;  break;
								case "INVALID_REQUEST" :  info.LocationStatus = "invalid request" ;  break;
							}
						}
					}
					else
					{
						info.LocationStatus = objResponse.StatusCode.ToString();
					}
				}
			}
		}

		/// <summary>
		/// Builds a formatted multi-line mailing address string from a <see cref="StructuredPostalAddress"/>.
		/// The output follows the standard US mailing address format:
		/// <code>
		/// Street
		/// City, State PostalCode
		/// Country
		/// </code>
		/// Fields are only included if non-empty (checked via <see cref="Sql.IsEmptyString"/>).
		/// </summary>
		/// <param name="adr">The structured postal address to format.</param>
		/// <returns>A formatted multi-line address string.</returns>
		public static string BuildFormattedAddress(StructuredPostalAddress adr)
		{
			StringBuilder sb = new StringBuilder();
			if ( !Sql.IsEmptyString(adr.Street) )
			{
				if( adr.Street.EndsWith("\n") )
					sb.Append(adr.Street);
				else
					sb.AppendLine(adr.Street);
			}
			if ( !Sql.IsEmptyString(adr.City) || !Sql.IsEmptyString(adr.State) || !Sql.IsEmptyString(adr.PostalCode) )
			{
				sb.Append(adr.City);
				if ( !Sql.IsEmptyString(adr.City) && (!Sql.IsEmptyString(adr.State) || !Sql.IsEmptyString(adr.PostalCode)) )
					sb.Append(", ");
				sb.Append(adr.State);
				if ( !Sql.IsEmptyString(adr.PostalCode) && (!Sql.IsEmptyString(adr.City) || !Sql.IsEmptyString(adr.State)) )
					sb.Append(" ");
				sb.Append(adr.PostalCode);
				sb.AppendLine();
			}
			if ( !Sql.IsEmptyString(adr.Country) )
			{
				sb.AppendLine(adr.Country);
			}
			return sb.ToString();
		}
	}
}
