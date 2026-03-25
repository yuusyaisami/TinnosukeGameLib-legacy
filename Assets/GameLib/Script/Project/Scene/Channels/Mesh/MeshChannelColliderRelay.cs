#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.Channel
{
    public sealed class MeshChannelColliderRelay : MonoBehaviour
    {
        sealed class HitState
        {
            public Collider2D? OtherCollider;
            public Vector2 Point;
            public Vector2 Normal;
            public Vector2 RelativeVelocity;
            public float ImpulseEstimate;
            public float PenetrationEstimate;
            public float FirstSeenTime;
        }

        readonly Dictionary<int, HitState> _activeHits = new();

        public void CaptureHits(List<MeshHitContactInfo> output)
        {
            output.Clear();
            foreach (var pair in _activeHits)
            {
                var state = pair.Value;
                if (state.OtherCollider == null)
                    continue;

                output.Add(new MeshHitContactInfo(
                    state.OtherCollider,
                    state.Point,
                    state.Normal,
                    state.RelativeVelocity,
                    state.ImpulseEstimate,
                    state.PenetrationEstimate,
                    Mathf.Max(0f, Time.time - state.FirstSeenTime)));
            }
        }

        public void ClearAll()
        {
            _activeHits.Clear();
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            UpsertCollision(collision);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            UpsertCollision(collision);
        }

        void OnCollisionExit2D(Collision2D collision)
        {
            Remove(collision.collider);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            UpsertTrigger(other);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            UpsertTrigger(other);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            Remove(other);
        }

        void UpsertCollision(Collision2D collision)
        {
            var other = collision.collider;
            if (other == null)
                return;

            var id = other.GetInstanceID();
            if (!_activeHits.TryGetValue(id, out var state))
            {
                state = new HitState
                {
                    FirstSeenTime = Time.time,
                };
                _activeHits[id] = state;
            }

            state.OtherCollider = other;
            state.RelativeVelocity = collision.relativeVelocity;

            if (collision.contactCount > 0)
            {
                var contact = collision.GetContact(0);
                state.Point = contact.point;
                state.Normal = contact.normal;
                state.PenetrationEstimate = Mathf.Max(0f, -contact.separation);

                var impulse = 0f;
                for (var i = 0; i < collision.contactCount; i++)
                {
                    var sample = collision.GetContact(i);
                    impulse += Mathf.Abs(sample.normalImpulse) + Mathf.Abs(sample.tangentImpulse);
                }

                state.ImpulseEstimate = impulse;
            }
            else
            {
                var point = other.ClosestPoint(transform.position);
                var normal = ((Vector2)transform.position - point);
                if (normal.sqrMagnitude > 0.0001f)
                    normal.Normalize();
                else
                    normal = Vector2.up;

                state.Point = point;
                state.Normal = normal;
                state.PenetrationEstimate = 0f;
                state.ImpulseEstimate = state.RelativeVelocity.magnitude;
            }
        }

        void UpsertTrigger(Collider2D other)
        {
            if (other == null)
                return;

            var id = other.GetInstanceID();
            if (!_activeHits.TryGetValue(id, out var state))
            {
                state = new HitState
                {
                    FirstSeenTime = Time.time,
                };
                _activeHits[id] = state;
            }

            var point = other.ClosestPoint(transform.position);
            var normal = ((Vector2)transform.position - point);
            if (normal.sqrMagnitude > 0.0001f)
                normal.Normalize();
            else
                normal = Vector2.up;

            var myBody = GetComponent<Rigidbody2D>();
            var otherBody = other.attachedRigidbody;

            state.OtherCollider = other;
            state.Point = point;
            state.Normal = normal;
            state.RelativeVelocity = (otherBody != null ? otherBody.linearVelocity : Vector2.zero) -
                                     (myBody != null ? myBody.linearVelocity : Vector2.zero);
            state.ImpulseEstimate = state.RelativeVelocity.magnitude;
            state.PenetrationEstimate = 0f;
        }

        void Remove(Collider2D? other)
        {
            if (other == null)
                return;

            _activeHits.Remove(other.GetInstanceID());
        }
    }
}
