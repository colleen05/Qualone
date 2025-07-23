using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using System.Collections.Generic;
using System;
using System.Linq;

using fennecs;

using Qualone.Core.Ecs;

namespace Qualone;

public record Ball();

public struct Position {
    public Vector2 Vec;

    public Position(float x, float y) {
        Vec.X = x;
        Vec.Y = y;
    }
}

public struct Velocity {
    public Vector2 Vec;
    
    public Velocity(float x, float y) {
        Vec.X = x;
        Vec.Y = y;
    }
}

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private World _ecsWorld;
    private List<Entity> _ballEntities;
    private Texture2D _ballTexture;

    public float RoomWidth = 1600;
    public float RoomHeight = 900;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);

        _graphics.PreferredBackBufferWidth = (int) RoomWidth;
        _graphics.PreferredBackBufferHeight = (int) RoomHeight;
        _graphics.IsFullScreen = false;

        Content.RootDirectory = "Content";

        Window.Title = "Qualone";
        
        IsMouseVisible = true;
    }

    public void ApplyBallVelocity(float deltaTime) {
        _ecsWorld
            .Query<Position, Velocity>()
            .Has<Ball>()
            .Stream()
            .Job((ref Position position, ref Velocity velocity) => {
                position.Vec += velocity.Vec * deltaTime;
            });
    }

    public void ApplyBallAirDampening(float deltaTime, float dampeningFactor) {
        _ecsWorld
            .Query<Velocity>()
            .Has<Ball>()
            .Stream()
            .Job((ref Velocity velocity) => {
                velocity.Vec *= MathF.Pow(dampeningFactor, deltaTime);
            });
    }

    public void BounceBallsOffWalls() {
        Rectangle roomBBox = new Rectangle(Point.Zero, new Point((int) RoomWidth, (int) RoomHeight));

        _ecsWorld
            .Query<Position, Velocity>()
            .Has<Ball>()
            .Stream()
            .Job((ref Position position, ref Velocity velocity) => {
                Rectangle ballBBox = new Rectangle(
                    new Point((int) position.Vec.X - 8, (int) position.Vec.Y - 8),
                    new Point(16, 16)
                );

                if(ballBBox.Intersects(roomBBox)) {
                    bool bounceX = true;
                    bool bounceY = true;

                    if (position.Vec.X < 0.0f)
                        position.Vec.X = 0.0f;
                    else if (position.Vec.X > RoomWidth)
                        position.Vec.X = RoomWidth;
                    else  bounceX = false;

                    if (position.Vec.Y < 0.0f)
                        position.Vec.Y = 0.0f;
                    else if (position.Vec.Y > RoomHeight)
                        position.Vec.Y = RoomHeight;
                    else bounceY = false;

                    if (bounceX) velocity.Vec.X *= -1.125f;
                    if (bounceY) velocity.Vec.Y *= -1.125f;
                }
            });
    }

    private void ApplyBallNudge(Vector2 mousePosition, int mode) {
        _ecsWorld
            .Query<Position, Velocity>()
            .Has<Ball>()
            .Stream()
            .Job((ref Position position, ref Velocity velocity) => {
                Vector2 diff = position.Vec - mousePosition;

                switch (mode) {
                    case 0: // Attract
                        if (diff.Length() < 128.0)
                            velocity.Vec -= diff * (128.0f - diff.Length()) / 64.0f;
                        break;
                    case 1: // Repel
                        if (diff.Length() < 128.0)
                            velocity.Vec += diff * (128.0f - diff.Length()) / 64.0f;
                        break;
                    default: // Trap
                        if (diff.Length() < 128.0)
                            velocity.Vec -= diff * (96.0f - diff.Length()) / 32.0f;
                        break;
                }
            });
    }

    public void SpawnBalls(int ballCount, float minSpeed, float maxSpeed) {
        Random rng = new Random();

        _ballEntities = Enumerable.Range(0, ballCount).Select(_ => {
            return _ecsWorld
                .Spawn()
                .Add<Ball>()
                .Add(new Color(
                    64 + (rng.Next() % 192),
                    64 + (rng.Next() % 192),
                    64 + (rng.Next() % 192)
                ))
                .Add(new Position(
                        8.0f + rng.NextSingle() * (RoomWidth - 8.0f),
                        8.0f + rng.NextSingle() * (RoomHeight - 8.0f)
                    )
                )
                .Add(new Velocity(
                        minSpeed + rng.NextSingle() * (maxSpeed - minSpeed) * (rng.Next() > int.MaxValue / 2 ? -1.0f : 1.0f),
                        minSpeed + rng.NextSingle() * (maxSpeed - minSpeed) * (rng.Next() > int.MaxValue / 2 ? -1.0f : 1.0f)
                    )
                );
        }).ToList();
    }

    public void DrawBalls() {
        _ecsWorld
            .Query<Position, Color>()
            .Has<Ball>()
            .Stream()
            .For((ref Position position, ref Color colour) => {
                _spriteBatch.Draw(_ballTexture, position.Vec - Vector2.One * 8.0f, colour);
            });
    }

    protected override void Initialize()
    {
        _ecsWorld = new World();

        SpawnBalls(2_000, 32.0f, 256.0f);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _ballTexture = Content.Load<Texture2D>("images/small_ball");
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        float deltaTime = (float) gameTime.ElapsedGameTime.TotalSeconds;

        ApplyBallVelocity(deltaTime);
        ApplyBallNudge(Mouse.GetState().Position.ToVector2(), (Mouse.GetState().LeftButton == ButtonState.Pressed) ? 2 : ((Mouse.GetState().RightButton == ButtonState.Pressed) ? 1 : 0));
        ApplyBallAirDampening(deltaTime, 0.5f);
        BounceBallsOffWalls();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        _spriteBatch.Begin();

        DrawBalls();

        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
