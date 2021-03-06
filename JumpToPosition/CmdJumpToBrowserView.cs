﻿#region Namespaces
using System.Collections.Generic;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
#endregion

namespace JumpToPosition
{
  [Transaction( TransactionMode.ReadOnly )]
  public class CmdJumpToBrowserView : IExternalCommand
  {
    /// <summary>
    /// Return a single preselected element
    /// or prompt user to select one.
    /// </summary>
    public static Result GetSingleSelectedElement(
      UIDocument uidoc,
      ref string message,
      out Element e )
    {
      Document doc = uidoc.Document;
      Selection sel = uidoc.Selection;
      ICollection<ElementId> ids = sel.GetElementIds();
      int n = ids.Count;

      e = null;

      if( 1 == n )
      {
        foreach( ElementId id in ids )
        {
          e = doc.GetElement( id );
        }
      }
      else if( 0 == n )
      {
        try
        {
          Reference r = sel.PickObject(
            ObjectType.Element, "Please select element "
              + "to view in external browser" );

          e = doc.GetElement( r.ElementId );
        }
        catch( OperationCanceledException )
        {
          return Result.Cancelled;
        }
      }
      else
      {
        message = "Please launch this command with "
          + "at most one pre-selected element";

        return Result.Failed;
      }
      return Result.Succeeded;
    }

    /// <summary>
    /// Return the level currently viewed.
    /// If that is not well-defined, return the level
    /// of the given element, else "<nil>".
    /// </summary>
    static string GetLevelFor(
      Element e,
      View view )
    {
      Document doc = e.Document;
      Element level = null;
      ElementId id = e.LevelId;
      if( ElementId.InvalidElementId != id )
      {
        level = doc.GetElement( id );
      }
      if( null == level )
      {
        level = view.GenLevel;
      }
      return null == level ? "<nil>" : level.Name;
    }

    /// <summary>
    /// Return the current Revit user name.
    /// </summary>
    static string GetUsername( Application app )
    {
      return app.Username;
    }

    /// <summary>
    /// Return the Revit application version string.
    /// </summary>
    static string GetAppVersionString( Application app )
    {
      return string.Format(
        "{0} {1}.{2}.{3}", app.VersionName,
        app.VersionNumber, app.SubVersionNumber,
        app.VersionBuild );
    }

    /// <summary>
    /// Return relevant view information (from
    /// Revit API documentation help file  sample).
    /// </summary>
    static string GetViewInfo( View view )
    {
      string message = string.Empty;

      // Get the name of the view

      message += "\r\nView name: " + view.Name;

      // The crop box sets display bounds of the view

      message += "\r\nCrop Box: "
        + Util.BoundingBoxString( view.CropBox );

      // Get the origin of the screen

      message += "\r\nOrigin: "
        + Util.PointString( view.Origin );

      // The bounds of the view in paper space in inches.

      message += "\r\nOutline: "
        + Util.BoundingBoxString( view.Outline );

      // The direction towards the right side of the screen

      message += "\r\nRight direction: "
        + Util.PointString( view.RightDirection );

      // The direction towards the top of the screen

      message += "\r\nUp direction: "
        + Util.PointString( view.UpDirection );

      // The direction towards the viewer

      message += "\r\nView direction: "
        + Util.PointString( view.ViewDirection );

      // The scale of the view

      message += "\r\nScale: " + view.Scale;

      // Scale is meaningless for Schedules
      //if( view.ViewType != ViewType.Schedule )
      //{
      //  int testScale = 5;
      //  //set the scale of the view
      //  view.Scale = testScale;
      //  message += "\r\nScale after set: " + view.Scale;
      //}

      return message;
    }

    /// <summary>
    /// Return relevant 3D view information (from
    /// Revit API documentation help file  sample).
    /// </summary>
    static string GetView3dInfo( View3D view3d )
    {
      string message = "3D View: ";

      // The position of the camera and view direction.

      ViewOrientation3D ori = view3d.GetOrientation();
      XYZ peye = ori.EyePosition;
      XYZ vforward = ori.ForwardDirection;
      XYZ vup = ori.UpDirection;

      message += string.Format(
        "\r\nCamera position: {0}"
        + "\r\nView direction: {1}"
        + "\r\nUp direction: {2}",
        Util.PointString( peye ),
        Util.PointString( vforward ),
        Util.PointString( vup ) );

      // Identifies whether the view is a perspective view. 

      if( view3d.IsPerspective )
      {
        message += "\r\nThe view is a perspective view.";
      }

      // The section box of the 3D view can cut the model.

      if( view3d.IsSectionBoxActive )
      {
        BoundingBoxXYZ sectionBox = view3d.GetSectionBox();

        // Note that the section box can be rotated 
        // and transformed. So the min/max corners 
        // coordinates relative to the model must be 
        // computed via the transform.

        Transform trf = sectionBox.Transform;

        XYZ max = sectionBox.Max; // Maximum coordinates (upper-right-front corner of the box before transform is applied).
        XYZ min = sectionBox.Min;  // Minimum coordinates (lower-left-rear corner of the box before transform is applied).

        // Transform the min and max to model coordinates

        XYZ maxInModelCoords = trf.OfPoint( max );
        XYZ minInModelCoords = trf.OfPoint( min );

        message += "\r\nView has an active section box: ";

        message += "\r\n'Maximum' coordinates: "
          + Util.PointString( maxInModelCoords );

        message += "\r\n'Minimum' coordinates: "
          + Util.PointString( minInModelCoords );
      }
      return message;
    }

    /// <summary>
    /// Return information to jump to selected element 
    /// in borwser view, e.g., element identifier, 
    /// current level viewed, view name, username, 
    /// Revit version, model version, current view 
    /// direction (x,y,z, front, up, ...).
    /// </summary>
    static string GetBrowserViewInfoFor(
      Element e,
      View view )
    {
      #region Generate HTML file
#if GENERATE_HTML_FILE
      string path = "C:/tmp/CmdJumpToBrowserView.html";
      using( StreamWriter s = new StreamWriter( path ) )
      {
        s.WriteLine( string.Format(
          "<html>\n<body>\n<p>{0}</p>\n</body>\n<html>",
          Util.ElementDescription( e ) ) );

        s.Close();
      }
      return path;
#endif // GENERATE_HTML_FILE
      #endregion // Generate HTML file

      Document doc = e.Document;
      Application app = doc.Application;
      DocumentVersion ver = Document.GetDocumentVersion( doc );

      string s = string.Format(
        "Element: {0}\r\n"
        + "Level: {1}\r\n"
        + "View: {2}\r\n"
        + "User: {3}\r\n"
        + "Revit: {4}\r\n"
        + "Model: guid {5} #saves {6}",
        Util.ElementDescription( e ),
        GetLevelFor( e, view ),
        view.Name,
        GetUsername( app ),
        GetAppVersionString( app ),
        ver.VersionGUID, ver.NumberOfSaves );

      s += GetViewInfo( view );

      //View3D view3d = view as View3D;

      //if( null != view3d )
      //{
      //  s += GetView3dInfo( view3d );
      //}

      return s;
    }

    public Result Execute(
      ExternalCommandData commandData,
      ref string message,
      ElementSet elements )
    {
      UIApplication uiapp = commandData.Application;
      UIDocument uidoc = uiapp.ActiveUIDocument;
      Application app = uiapp.Application;
      Document doc = uidoc.Document;
      View view = doc.ActiveView;
      Element e;

      Result r = GetSingleSelectedElement(
        uidoc, ref message, out e );

      if( Result.Succeeded == r )
      {
        string browser_view_info
          = GetBrowserViewInfoFor( e, view );

        using( TaskDialog td = new TaskDialog( 
          "Jump to Browser View" ) )
        {
          td.MainInstruction = "HTTP request "
            + "or web socket invocation data";

          td.MainContent = browser_view_info;

          td.Show();
        }
      }
      return r;
    }
  }
}
