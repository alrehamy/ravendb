package raven.client;

import raven.abstractions.basic.EventHandler;
import raven.abstractions.basic.VoidArgs;

/**
 * Provide a way for interested party to tell whatever implementers have been disposed
 */
public interface IDisposalNotification extends AutoCloseable {

  /**
   * Called after dispose is completed
   * @param event
   */
  public void addAfterDisposeEventHandler(EventHandler<VoidArgs> event);

  /**
   * Remove event handler
   * @param event
   */
  public void removeAfterDisposeEventHandler(EventHandler<VoidArgs> event);


  /**
   * Whatever the instance has been disposed
   * @return
   */
  public boolean getWasDisposed();

}
